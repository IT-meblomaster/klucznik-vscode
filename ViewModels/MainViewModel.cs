using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MojaAplikacja.Models;
using MojaAplikacja.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace MojaAplikacja.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan ScanWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SuccessDisplayWindow = TimeSpan.FromSeconds(5);

    private readonly OracleTestService _oracleService = new();
    private readonly KeyService _keyService = new();
    private readonly DatabaseSettingsService _databaseSettingsService = new();

    private PersonResult? _pendingPerson;
    private KeyItem? _pendingKey;
    private DateTime? _firstScanAt;
    private CancellationTokenSource? _scanTimeoutCts;
    private CancellationTokenSource? _successDisplayCts;

    private DbSettingsSectionSnapshot? _oracleSnapshot;
    private DbSettingsSectionSnapshot? _mySqlSnapshot;

    public MainViewModel()
    {
        LoadDatabaseSettings();
    }

    [ObservableProperty]
    private string cardNumber = string.Empty;

    [ObservableProperty]
    private string firstName = string.Empty;

    [ObservableProperty]
    private string lastName = string.Empty;

    [ObservableProperty]
    private string employeeCardDisplay = string.Empty;

    [ObservableProperty]
    private string currentKeyName = string.Empty;

    [ObservableProperty]
    private string currentKeyDescription = string.Empty;

    [ObservableProperty]
    private string currentKeyRfidStatus = string.Empty;

    [ObservableProperty]
    private string status = "Przyłóż kartę pracownika lub klucza.";

    [ObservableProperty]
    private string keysStatus = "Gotowe.";

    [ObservableProperty]
    private KeyItem? selectedKey;

    [ObservableProperty]
    private bool canEditOrDeleteKey;

    [ObservableProperty]
    private bool canAssignRfid;

    [ObservableProperty]
    private bool canRemoveRfid;

    [ObservableProperty]
    private DbSettingsSection oracleSettings = new();

    [ObservableProperty]
    private DbSettingsSection mySqlSettings = new();

    [ObservableProperty]
    private string settingsStatus = "Gotowe.";

    public ObservableCollection<KeyItem> Keys { get; } = new();
    public ObservableCollection<ScannerLogItem> ScannerLogs { get; } = new();

    partial void OnSelectedKeyChanged(KeyItem? value)
    {
        CanEditOrDeleteKey = value is not null;
        CanAssignRfid = value is not null && !value.HasRfid;
        CanRemoveRfid = value is not null && value.HasRfid;
    }

    public void LoadDatabaseSettings()
    {
        try
        {
            var loaded = _databaseSettingsService.Load();

            OracleSettings = loaded.Oracle;
            MySqlSettings = loaded.MySql;

            _oracleSnapshot = OracleSettings.CreateSnapshot();
            _mySqlSnapshot = MySqlSettings.CreateSnapshot();

            SettingsStatus = "Wczytano ustawienia.";
        }
        catch (Exception ex)
        {
            SettingsStatus = $"Błąd wczytywania ustawień: {ex.Message}";
        }
    }

    public void BeginEditOracle()
    {
        _oracleSnapshot = OracleSettings.CreateSnapshot();
        OracleSettings.IsEditing = true;
        SettingsStatus = "Edycja ustawień Oracle.";
    }

    public void BeginEditMySql()
    {
        _mySqlSnapshot = MySqlSettings.CreateSnapshot();
        MySqlSettings.IsEditing = true;
        SettingsStatus = "Edycja ustawień mySQL.";
    }

    public void CancelEditOracle()
    {
        if (_oracleSnapshot is not null)
        {
            OracleSettings.Restore(_oracleSnapshot);
        }

        OracleSettings.IsEditing = false;
        SettingsStatus = "Anulowano zmiany Oracle.";
    }

    public void CancelEditMySql()
    {
        if (_mySqlSnapshot is not null)
        {
            MySqlSettings.Restore(_mySqlSnapshot);
        }

        MySqlSettings.IsEditing = false;
        SettingsStatus = "Anulowano zmiany mySQL.";
    }

    public void SaveOracle()
    {
        try
        {
            _databaseSettingsService.Save(OracleSettings);
            OracleSettings.IsEditing = false;
            _oracleSnapshot = OracleSettings.CreateSnapshot();
            SettingsStatus = "Zapisano ustawienia Oracle.";
        }
        catch (Exception ex)
        {
            SettingsStatus = $"Błąd zapisu Oracle: {ex.Message}";
        }
    }

    public void SaveMySql()
    {
        try
        {
            _databaseSettingsService.Save(MySqlSettings);
            MySqlSettings.IsEditing = false;
            _mySqlSnapshot = MySqlSettings.CreateSnapshot();
            SettingsStatus = "Zapisano ustawienia mySQL.";
        }
        catch (Exception ex)
        {
            SettingsStatus = $"Błąd zapisu mySQL: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LookupCardAsync()
    {
        var scannedValue = CardNumber?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(scannedValue))
        {
            Status = "Brak numeru karty.";
            return;
        }

        await ProcessScannerCodeAsync(scannedValue);
    }

    public async Task ProcessScannerCodeAsync(string scannedValue)
    {
        CancelSuccessDisplayClear();

        var code = scannedValue.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        if (_firstScanAt.HasValue && DateTime.Now - _firstScanAt.Value > ScanWindow)
        {
            ClearScannerState("Przekroczono 10 sekund. Wyczyściłem dane.");
        }

        var person = await _oracleService.FindPersonByCardAsync(code);
        var key = await _keyService.GetKeyByRfidAsync(code);

        if (person is null && key is null)
        {
            Status = $"Nie rozpoznano skanu: {code}";
            return;
        }

        if (person is not null && key is not null)
        {
            Status = $"Skan {code} pasuje jednocześnie do pracownika i klucza.";
            return;
        }

        if (_pendingPerson is null && _pendingKey is null)
        {
            _firstScanAt = DateTime.Now;

            if (person is not null)
            {
                SetPendingPerson(person);
                Status = "Zeskanowano pracownika. Oczekiwanie na klucz...";
            }
            else if (key is not null)
            {
                SetPendingKey(key);
                Status = "Zeskanowano klucz. Oczekiwanie na pracownika...";
            }

            StartScanTimeout();
            return;
        }

        if (person is not null)
        {
            if (_pendingPerson is null)
            {
                SetPendingPerson(person);
            }
            else
            {
                _firstScanAt = DateTime.Now;
                SetPendingPerson(person);
                Status = "Zmieniono pracownika. Oczekiwanie na klucz...";
                StartScanTimeout();
                return;
            }
        }

        if (key is not null)
        {
            if (_pendingKey is null)
            {
                SetPendingKey(key);
            }
            else
            {
                _firstScanAt = DateTime.Now;
                SetPendingKey(key);
                Status = "Zmieniono klucz. Oczekiwanie na pracownika...";
                StartScanTimeout();
                return;
            }
        }

        if (_pendingPerson is not null && _pendingKey is not null)
        {
            CancelScanTimeout();

            try
            {
                var result = await _keyService.RegisterIssueOrReturnAsync(_pendingKey, _pendingPerson);

                AddScannerLog(result.Message);
                Status = result.Message;

                _pendingPerson = null;
                _pendingKey = null;
                _firstScanAt = null;

                StartSuccessDisplayClear();
                await RefreshKeysAsync();
            }
            catch (Exception ex)
            {
                ClearScannerState($"Błąd operacji: {ex.Message}");
            }
        }
    }

    public async Task RefreshKeysAsync()
    {
        try
        {
            var currentSelectedId = SelectedKey?.Id;

            Keys.Clear();
            KeysStatus = "Ładowanie kluczy...";

            var items = await _keyService.GetKeysAsync();

            foreach (var item in items)
            {
                Keys.Add(item);
            }

            if (currentSelectedId.HasValue)
            {
                SelectedKey = Keys.FirstOrDefault(x => x.Id == currentSelectedId.Value);
            }

            KeysStatus = $"Wczytano {Keys.Count} kluczy.";
        }
        catch (Exception ex)
        {
            Keys.Clear();
            KeysStatus = $"Błąd MariaDB: {ex.Message}";
        }
    }

    public async Task CreateKeyAsync(string name, string? description)
    {
        try
        {
            await _keyService.InsertAsync(name, description);
            KeysStatus = "Dodano klucz.";
            await RefreshKeysAsync();
        }
        catch (Exception ex)
        {
            KeysStatus = $"Błąd dodawania: {ex.Message}";
        }
    }

    public async Task EditKeyAsync(uint id, string name, string? description, bool removeRfid)
    {
        try
        {
            await _keyService.UpdateAsync(id, name, description, removeRfid);
            KeysStatus = "Zaktualizowano klucz.";
            await RefreshKeysAsync();
            SelectedKey = Keys.FirstOrDefault(x => x.Id == id);
        }
        catch (Exception ex)
        {
            KeysStatus = $"Błąd edycji: {ex.Message}";
        }
    }

    public async Task DeleteSelectedKeyAsync()
    {
        if (SelectedKey is null)
        {
            KeysStatus = "Nie wybrano klucza.";
            return;
        }

        try
        {
            var id = SelectedKey.Id;
            await _keyService.DeleteAsync(id);
            KeysStatus = "Usunięto klucz.";
            SelectedKey = null;
            await RefreshKeysAsync();
        }
        catch (Exception ex)
        {
            KeysStatus = $"Błąd usuwania: {ex.Message}";
        }
    }

    public async Task AssignRfidToSelectedKeyAsync(string scannedRfid)
    {
        if (SelectedKey is null)
        {
            KeysStatus = "Nie wybrano klucza.";
            return;
        }

        var value = scannedRfid.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            KeysStatus = "Brak odczytanego RFID.";
            return;
        }

        try
        {
            var existingKeyName = await _keyService.GetKeyNameByRfidAsync(value);

            if (!string.IsNullOrWhiteSpace(existingKeyName) &&
                !string.Equals(existingKeyName, SelectedKey.Name, StringComparison.OrdinalIgnoreCase))
            {
                KeysStatus = $"RFID przypisany do klucza {existingKeyName}";
                return;
            }

            var id = SelectedKey.Id;
            await _keyService.AssignRfidAsync(id, value);
            KeysStatus = $"Przypisano RFID do klucza: {SelectedKey.Name}";
            await RefreshKeysAsync();
            SelectedKey = Keys.FirstOrDefault(x => x.Id == id);
        }
        catch (Exception ex)
        {
            KeysStatus = $"Błąd przypisywania RFID: {ex.Message}";
        }
    }

    public async Task RemoveRfidFromSelectedKeyAsync()
    {
        if (SelectedKey is null)
        {
            KeysStatus = "Nie wybrano klucza.";
            return;
        }

        if (!SelectedKey.HasRfid)
        {
            KeysStatus = "Wybrany klucz nie ma przypisanego RFID.";
            return;
        }

        try
        {
            var id = SelectedKey.Id;
            await _keyService.RemoveRfidAsync(id);
            KeysStatus = $"Usunięto RFID z klucza: {SelectedKey.Name}";
            await RefreshKeysAsync();
            SelectedKey = Keys.FirstOrDefault(x => x.Id == id);
        }
        catch (Exception ex)
        {
            KeysStatus = $"Błąd usuwania RFID: {ex.Message}";
        }
    }

    public void ClearCardInput()
    {
        CardNumber = string.Empty;
    }

    private void SetPendingPerson(PersonResult person)
    {
        _pendingPerson = person;
        FirstName = person.FirstName;
        LastName = person.LastName;
        EmployeeCardDisplay = person.CardNumber;
    }

    private void SetPendingKey(KeyItem key)
    {
        _pendingKey = key;
        CurrentKeyName = key.Name;
        CurrentKeyDescription = key.Description ?? string.Empty;
        CurrentKeyRfidStatus = key.RfidStatus;
    }

    private void ClearScannerPanels()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        EmployeeCardDisplay = string.Empty;

        CurrentKeyName = string.Empty;
        CurrentKeyDescription = string.Empty;
        CurrentKeyRfidStatus = string.Empty;
    }

    private void ClearScannerState(string statusMessage)
    {
        CancelScanTimeout();
        CancelSuccessDisplayClear();
        _pendingPerson = null;
        _pendingKey = null;
        _firstScanAt = null;
        ClearScannerPanels();
        Status = statusMessage;
    }

    private void StartScanTimeout()
    {
        CancelScanTimeout();
        _scanTimeoutCts = new CancellationTokenSource();
        var token = _scanTimeoutCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ScanWindow, token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_pendingPerson is not null || _pendingKey is not null)
                    {
                        ClearScannerState("Przekroczono 10 sekund. Wyczyściłem dane.");
                    }
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void CancelScanTimeout()
    {
        if (_scanTimeoutCts is null)
        {
            return;
        }

        _scanTimeoutCts.Cancel();
        _scanTimeoutCts.Dispose();
        _scanTimeoutCts = null;
    }

    private void StartSuccessDisplayClear()
    {
        CancelSuccessDisplayClear();
        _successDisplayCts = new CancellationTokenSource();
        var token = _successDisplayCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SuccessDisplayWindow, token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ClearScannerPanels();
                    Status = "Przyłóż kartę pracownika lub klucza.";
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void CancelSuccessDisplayClear()
    {
        if (_successDisplayCts is null)
        {
            return;
        }

        _successDisplayCts.Cancel();
        _successDisplayCts.Dispose();
        _successDisplayCts = null;
    }

    private void AddScannerLog(string message)
    {
        ScannerLogs.Insert(0, new ScannerLogItem
        {
            Timestamp = DateTime.Now,
            Message = message
        });
    }
}