using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Klucznik.Models;

namespace Klucznik.Services;

public class ScanResolution
{
    public PersonResult? Person { get; init; }
    public KeyItem? Key { get; init; }
    public bool PersonLookupFailed { get; init; }
    public bool KeyLookupFailed { get; init; }
}

public class SyncCoordinator : IDisposable
{
    private readonly OracleTestService _oracleService;
    private readonly KeyService _keyService;
    private readonly LocalCacheService _localCache;

    private readonly Timer _syncTimer;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public event EventHandler<string>? SyncMessage;
    public event EventHandler? StateChanged;

    public bool IsMariaDbOnline { get; private set; } = true;
    public bool IsPostgreSqlOnline { get; private set; } = true;
    public int PendingEventsCount { get; private set; }

    public SyncCoordinator(OracleTestService oracleService, KeyService keyService, LocalCacheService localCache)
    {
        _oracleService = oracleService;
        _keyService = keyService;
        _localCache = localCache;

        PendingEventsCount = _localCache.CountUnsyncedEvents();

        // Pierwszy przebieg po 10s (żeby appka zdążyła w pełni wystartować),
        // potem co 20s.
        _syncTimer = new Timer(async _ => await TrySyncPendingEventsAsync(), null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
    }

    // Wołane po każdym udanym RefreshKeysAsync() z MariaDB - odświeża lokalną
    // kopię, żeby offline'owe skany kluczy miały aktualny stan.
    public void RefreshLocalCache(IEnumerable<KeyItem> keys)
    {
        _localCache.ReplaceKeysSnapshot(keys);
    }

    public async Task<ScanResolution> ResolveScanAsync(string code)
    {
        PersonResult? person = null;
        bool personFailed = false;

        try
        {
            person = await _oracleService.FindPersonByCardAsync(code);
            IsPostgreSqlOnline = true;
        }
        catch
        {
            personFailed = true;
            IsPostgreSqlOnline = false;
        }

        KeyItem? key = null;
        bool keyFailed = false;

        try
        {
            key = await _keyService.GetKeyByRfidAsync(code);
            IsMariaDbOnline = true;
        }
        catch
        {
            keyFailed = true;
            IsMariaDbOnline = false;
            key = _localCache.FindKeyByRfid(code);
        }

        // Postgres offline i kod nie pasuje do żadnego klucza -> zakładamy,
        // że to karta pracownika, tylko nie możemy jej teraz zweryfikować.
        if (personFailed && key is null)
        {
            person = new PersonResult
            {
                CardNumber = code,
                FirstName = string.Empty,
                LastName = "(offline - do zweryfikowania)",
                IsOfflinePlaceholder = true
            };
        }

        StateChanged?.Invoke(this, EventArgs.Empty);

        return new ScanResolution
        {
            Person = person,
            Key = key,
            PersonLookupFailed = personFailed,
            KeyLookupFailed = keyFailed
        };
    }

    public async Task<KeyLoanOperationResult> RegisterAsync(KeyItem key, PersonResult person)
    {
        try
        {
            var result = await _keyService.RegisterIssueOrReturnAsync(key, person);
            IsMariaDbOnline = true;

            _localCache.ApplyLocalToggle(
                (int)key.Id,
                result.IsIssue,
                result.IsIssue ? $"{person.FirstName} {person.LastName}".Trim() : null,
                result.IsIssue ? DateTime.Now : null);

            return result;
        }
        catch
        {
            IsMariaDbOnline = false;

            // Ten sam toggle co robi serwer: jeśli klucz był wydany -> to zwrot,
            // jeśli nie był -> wydanie. Opieramy się na ostatnio znanym stanie
            // (z cache'u odświeżanego przy każdym udanym GetKeysAsync/GetKeyByRfid).
            var isIssueNow = !key.IsIssued;

            _localCache.ApplyLocalToggle(
                (int)key.Id,
                isIssueNow,
                isIssueNow ? $"{person.FirstName} {person.LastName}".Trim() : null,
                isIssueNow ? DateTime.Now : null);

            _localCache.EnqueueEvent(new PendingKeyEvent
            {
                KeyId = key.Id,
                KeyName = key.Name,
                KeyBuilding = key.Building,
                RfidTagId = key.CurrentRfidTagId,
                Action = isIssueNow ? "ISSUE" : "RETURN",
                PersonCard = person.CardNumber,
                PersonFirstName = person.FirstName,
                PersonLastName = person.LastName,
                PersonOffline = person.IsOfflinePlaceholder,
                CreatedAt = DateTime.Now
            });

            PendingEventsCount = _localCache.CountUnsyncedEvents();
            StateChanged?.Invoke(this, EventArgs.Empty);

            var keyDisplay = string.IsNullOrWhiteSpace(key.Building)
                ? key.Name
                : $"{key.Name} ({key.Building})";

            var message = isIssueNow
                ? $"Wydano klucz (offline): {keyDisplay} -> {person.FirstName} {person.LastName}".Trim()
                : $"Zwrócono klucz (offline): {keyDisplay} <- {person.FirstName} {person.LastName}".Trim();

            return new KeyLoanOperationResult
            {
                IsIssue = isIssueNow,
                IsReturn = !isIssueNow,
                Message = message + " — zapisano lokalnie, oczekuje na synchronizację."
            };
        }
    }

public async Task TrySyncPendingEventsAsync()
{
    if (!await _syncLock.WaitAsync(0))
        return; // synchronizacja już trwa

    try
    {
        List<PendingKeyEvent> pending;

        try
        {
            pending = _localCache.GetUnsyncedEvents();
        }
        catch (Exception ex)
        {
            SyncMessage?.Invoke(this, $"Błąd odczytu lokalnej kolejki: {ex.Message}");
            return;
        }

        if (pending.Count == 0)
            return;

        foreach (var ev in pending)
        {
            if (ev.PersonOffline)
            {
                try
                {
                    var resolved = await _oracleService.FindPersonByCardAsync(ev.PersonCard);

                    if (resolved is not null)
                    {
                        ev.PersonFirstName = resolved.FirstName;
                        ev.PersonLastName = resolved.LastName;
                        _localCache.UpdatePersonNameIfOffline(ev.Id, resolved.FirstName, resolved.LastName);
                    }
                }
                catch
                {
                    // nadal offline / nadal nierozpoznana - wyślij z tym co mamy
                }
            }

            var syntheticKey = new KeyItem
            {
                Id = ev.KeyId,
                Name = ev.KeyName,
                Building = ev.KeyBuilding,
                CurrentRfidTagId = ev.RfidTagId
            };

            var syntheticPerson = new PersonResult
            {
                CardNumber = ev.PersonCard,
                FirstName = ev.PersonFirstName,
                LastName = ev.PersonLastName
            };

            try
            {
                var result = await _keyService.RegisterIssueOrReturnAsync(
                    syntheticKey, syntheticPerson, ev.CreatedAt);

                var expectedIssue = ev.Action == "ISSUE";
                var conflict = result.IsIssue != expectedIssue;

                _localCache.MarkSynced(ev.Id, conflict);

                SyncMessage?.Invoke(this,
                    conflict
                        ? $"Zsynchronizowano zdarzenie z ROZBIEŻNOŚCIĄ (klucz {ev.KeyName}) - sprawdź logi."
                        : $"Zsynchronizowano zaległe zdarzenie: {ev.KeyName}.");

                IsMariaDbOnline = true;
            }
            catch (Exception ex)
            {
                _localCache.MarkFailedAttempt(ev.Id, ex.Message);
                IsMariaDbOnline = false;
                break; // zachowaj kolejność - reszta poczeka do następnej próby
            }
        }

        PendingEventsCount = _localCache.CountUnsyncedEvents();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    finally
    {
        _syncLock.Release();
    }
}

    public void Dispose()
    {
        _syncTimer.Dispose();
        _syncLock.Dispose();
    }
}