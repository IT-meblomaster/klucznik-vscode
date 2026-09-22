using Klucznik.Models;
using MySqlConnector;

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
    public DateTime? AccessSnapshotTime => _localCache.GetAccessSnapshotTime();

    public SyncCoordinator(OracleTestService oracleService, KeyService keyService, LocalCacheService localCache)
    {
        _oracleService = oracleService;
        _keyService = keyService;
        _localCache = localCache;
        PendingEventsCount = _localCache.CountUnsyncedEvents();
        _syncTimer = new Timer(async _ => await TrySyncPendingEventsAsync(), null,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(20));
    }

    // Synchronizację i wydawanie serializuje jedna blokada. Odświeżenie UI nie
    // nadpisuje stanu lokalnego, jeżeli trwa zapis zdarzenia.
    public void RefreshLocalCache(IEnumerable<KeyItem> keys)
    {
        if (!_syncLock.Wait(0)) return;
        try { _localCache.ReplaceKeysSnapshot(keys); }
        finally { _syncLock.Release(); }
    }

    private static bool IsConnectionFailure(Exception exception) =>
        exception is TimeoutException ||
        exception is MySqlException mysql &&
        mysql.Number is 0 or -1 or 1040 or 1042 or 1047 or 1158 or 1159 or 1160 or 1161 or 2002 or 2003 or 2006 or 2013;

    public async Task<ScanResolution> ResolveScanAsync(string code)
    {
        PersonResult? person = null;
        KeyItem? key = null;
        bool personFailed = false, keyFailed = false;
        try
        {
            person = await _oracleService.FindPersonByCardAsync(code);
            IsPostgreSqlOnline = true;
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException || ex is TimeoutException)
        {
            personFailed = true;
            IsPostgreSqlOnline = false;
        }
        try
        {
            key = await _keyService.GetKeyByRfidAsync(code);
            IsMariaDbOnline = true;
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            keyFailed = true;
            IsMariaDbOnline = false;
            key = _localCache.FindKeyByRfid(code);
        }
        // Niezsynchronizowane lokalne operacje mają pierwszeństwo przed stanem
        // serwera, ale nie przed centralną listą uprawnień.
        if (key is not null && _localCache.GetUnsyncedEvents().Any(e => e.KeyId == key.Id))
            key = _localCache.FindKeyByRfid(code) ?? key;
        if (personFailed && key is null)
            person = new PersonResult {
                CardNumber = code, LastName = "(offline - do zweryfikowania)", IsOfflinePlaceholder = true
            };
        StateChanged?.Invoke(this, EventArgs.Empty);
        return new ScanResolution { Person = person, Key = key, PersonLookupFailed = personFailed, KeyLookupFailed = keyFailed };
    }

    public async Task<KeyLoanOperationResult> RegisterAsync(KeyItem key, PersonResult person)
    {
        await _syncLock.WaitAsync();
        try
        {
            // Najpierw opróżniamy kolejkę. Nie wysyłamy nowego zdarzenia przed starszym.
            bool online;
            try
            {
                await SyncQueueAsync();
                online = true;
            }
            catch (Exception ex) when (IsConnectionFailure(ex))
            {
                online = false;
                IsMariaDbOnline = false;
            }
            bool issue = !key.IsIssued;
            var id = Guid.NewGuid();
            var at = DateTime.Now;
            if (online)
            {
                try
                {
                    // Centralny odczyt i sprawdzenie w tej samej transakcji co wydanie.
                    var result = await _keyService.RegisterIssueOrReturnAsync(key, person, id, issue, at);
                    IsMariaDbOnline = true;
                    _localCache.ApplyLocalToggle((int)key.Id, result.IsIssue,
                        result.IsIssue ? $"{person.FirstName} {person.LastName}".Trim() : null,
                        result.IsIssue ? at : null);
                    return result;
                }
                catch (Exception ex) when (IsConnectionFailure(ex))
                {
                    IsMariaDbOnline = false;
                }
            }

            // Błędy uprawnień, schematu i zapisu lokalnego nie trafiają do tego trybu.
            bool restricted = false;
            DateTime? policyAt = null;
            if (issue)
            {
                var decision = _localCache.CheckOfflineIssue(key.Id, person.CardNumber);
                restricted = decision.Restricted;
                policyAt = decision.SyncedAtUtc;
            }
            _localCache.EnqueueEvent(new PendingKeyEvent {
                Id = id, KeyId = key.Id, KeyName = key.Name, KeyBuilding = key.Building,
                RfidTagId = key.CurrentRfidTagId, Action = issue ? "ISSUE" : "RETURN",
                PersonCard = person.CardNumber, PersonFirstName = person.FirstName,
                PersonLastName = person.LastName, PersonOffline = person.IsOfflinePlaceholder,
                CreatedAt = at, PolicySyncedAtUtc = policyAt, WasRestricted = restricted
            });
            var message = issue
                ? $"Wydano klucz: {key.KeyWithBuildingDisplay} -> {person.FirstName} {person.LastName}"
                : $"Zwrócono klucz: {key.KeyWithBuildingDisplay} <- {person.FirstName} {person.LastName}";
            return new KeyLoanOperationResult {
                IsIssue = issue, IsReturn = !issue, IsRestricted = restricted,
                Message = message.Trim() + (restricted ? " [klucz specjalny]" : "") +
                    " [offline — zapisano lokalnie]" +
                    (issue ? $" [kopia uprawnień: {policyAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss}]" : "")
            };
        }
        finally
        {
            PendingEventsCount = _localCache.CountUnsyncedEvents();
            _syncLock.Release();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task SyncQueueAsync()
    {
        var conflictingKeys = new HashSet<uint>();
        foreach (var ev in _localCache.GetUnsyncedEvents())
        {
            if (ev.PersonOffline)
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
                catch (Exception ex) when (ex is Npgsql.NpgsqlException || ex is TimeoutException)
                { /* Historia zachowuje numer karty także bez nazwiska. */ }
            try
            {
                var result = await _keyService.RegisterIssueOrReturnAsync(
                    new KeyItem { Id = ev.KeyId, Name = ev.KeyName, Building = ev.KeyBuilding },
                    new PersonResult { CardNumber = ev.PersonCard, FirstName = ev.PersonFirstName, LastName = ev.PersonLastName },
                    ev.Id, ev.Action == "ISSUE", ev.CreatedAt, replay: true,
                    offlinePolicyAt: ev.PolicySyncedAtUtc, offlineRestricted: ev.WasRestricted,
                    dependentConflict: ev.SyncConflict || conflictingKeys.Contains(ev.KeyId));
                if (result.HasConflict)
                {
                    conflictingKeys.Add(ev.KeyId);
                    _localCache.MarkDependentConflicts(ev.KeyId);
                }
                _localCache.MarkSynced(ev.Id, result.HasConflict);
                SyncMessage?.Invoke(this, result.HasConflict ? result.Message : $"Zsynchronizowano zdarzenie: {ev.KeyName}.");
            }
            catch (Exception ex)
            {
                _localCache.MarkFailedAttempt(ev.Id, ex.Message);
                throw;
            }
        }
    }

    public async Task TrySyncPendingEventsAsync()
    {
        if (!await _syncLock.WaitAsync(0)) return;
        try
        {
            // Kopia jest zastępowana atomowo, tylko po kompletnym odczycie bazy.
            _localCache.ReplaceAccessSnapshot(await _keyService.GetAccessSnapshotAsync());
            await SyncQueueAsync();
            _localCache.ReplaceKeysSnapshot(await _keyService.GetKeysAsync());
            IsMariaDbOnline = true;
        }
        catch (Exception ex)
        {
            if (IsConnectionFailure(ex)) IsMariaDbOnline = false;
            SyncMessage?.Invoke(this, $"Synchronizacja: {ex.Message}");
        }
        finally
        {
            PendingEventsCount = _localCache.CountUnsyncedEvents();
            _syncLock.Release();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _syncTimer.Dispose();
}
