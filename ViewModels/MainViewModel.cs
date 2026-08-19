using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klucznik.Models;
using Klucznik.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace Klucznik.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string AllUsersOption = "Wszyscy";
    private const string AllKeysOption = "Wszystkie";
    private const string AllBuildingsOption = "Wszystkie";

    private static readonly TimeSpan ScanWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SuccessDisplayWindow = TimeSpan.FromSeconds(5);

    private readonly OracleTestService _oracleService = new();
    private readonly KeyService _keyService = new();
    private readonly DatabaseSettingsService _databaseSettingsService = new();
    private readonly AdminPasswordService _adminPasswordService = new();

    private readonly List<KeyLoanReportItem> _allLoanReports = new();

    private PersonResult? _pendingPerson;
    private KeyItem? _pendingKey;
    private DateTime? _firstScanAt;
    private CancellationTokenSource? _scanTimeoutCts;
    private CancellationTokenSource? _successDisplayCts;

    private DbSettingsSectionSnapshot? _oracleSnapshot;
    private DbSettingsSectionSnapshot? _mySqlSnapshot;
    public ObservableCollection<BuildingItem> Buildings { get; } = new();

    [ObservableProperty]
    private BuildingItem? selectedBuilding;

    [ObservableProperty]
    private bool canEditOrDeleteBuilding;
    public MainViewModel()
    {
        LoadDatabaseSettings();
        LoadScannerSettings();
        LoadAdminPasswordStatus();

        ReportUsers.Add(AllUsersOption);
        ReportKeys.Add(AllKeysOption);
        ReportBuildings.Add(AllBuildingsOption);

        SelectedReportUser = AllUsersOption;
        SelectedReportKey = AllKeysOption;
        SelectedReportBuilding = AllBuildingsOption;
    }
    partial void OnSelectedBuildingChanged(BuildingItem? value)
    {
        CanEditOrDeleteBuilding = value is not null;
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
    private string currentKeyBuilding = string.Empty;

    [ObservableProperty]
    private string currentKeyDescription = string.Empty;

    [ObservableProperty]
    private string currentKeyHanger = string.Empty;

    [ObservableProperty]
    private string currentKeyRfidStatus = string.Empty;

    public string CurrentKeyDisplay =>
        string.IsNullOrWhiteSpace(CurrentKeyName)
            ? string.Empty
            : string.IsNullOrWhiteSpace(CurrentKeyBuilding)
                ? CurrentKeyName
                : $"{CurrentKeyName} ({CurrentKeyBuilding})";

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
    private ScannerSettingsSection scannerSettings = new();

    [ObservableProperty]
    private string settingsStatus = "Gotowe.";

    [ObservableProperty]
    private bool adminPasswordExists;

    [ObservableProperty]
    private string adminPasswordStatus = "Gotowe.";

    [ObservableProperty]
    private DateTime? reportDateFrom;

    [ObservableProperty]
    private DateTime? reportDateTo;

    [ObservableProperty]
    private string selectedReportUser = AllUsersOption;

    [ObservableProperty]
    private string selectedReportKey = AllKeysOption;

    [ObservableProperty]
    private string selectedReportBuilding = AllBuildingsOption;

    [ObservableProperty]
    private string reportStatus = "Gotowe.";

    public string AdminPasswordHeader => AdminPasswordExists
        ? "Hasło administratora - zmiana"
        : "Hasło administratora - pierwsze ustawienie";

    public ObservableCollection<KeyItem> Keys { get; } = new();
    public ObservableCollection<InventoryKeyGroup> InventoryGroups { get; } = new();
    public ObservableCollection<ScannerLogItem> ScannerLogs { get; } = new();
    public ObservableCollection<KeyLoanReportItem> LoanReports { get; } = new();
    public ObservableCollection<string> ReportUsers { get; } = new();
    public ObservableCollection<string> ReportKeys { get; } = new();
    public ObservableCollection<string> ReportBuildings { get; } = new();

    partial void OnCurrentKeyNameChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentKeyDisplay));
    }

    partial void OnCurrentKeyBuildingChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentKeyDisplay));
    }

    partial void OnSelectedKeyChanged(KeyItem? value)
    {
        CanEditOrDeleteKey = value is not null;
        CanAssignRfid = value is not null && !value.HasRfid;
        CanRemoveRfid = value is not null && value.HasRfid;
    }

    partial void OnAdminPasswordExistsChanged(bool value)
    {
        OnPropertyChanged(nameof(AdminPasswordHeader));
    }

    partial void OnReportDateFromChanged(DateTime? value) => ApplyLoanReportFilters();
    partial void OnReportDateToChanged(DateTime? value) => ApplyLoanReportFilters();
    partial void OnSelectedReportUserChanged(string value) => ApplyLoanReportFilters();
    partial void OnSelectedReportKeyChanged(string value) => ApplyLoanReportFilters();
    partial void OnSelectedReportBuildingChanged(string value) => ApplyLoanReportFilters();

    public async Task<List<BuildingItem>> GetBuildingsAsync()
    {
        return await _keyService.GetBuildingsAsync();
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

    public void LoadScannerSettings()
    {
        try
        {
            ScannerSettings = _databaseSettingsService.LoadScannerSettings();
        }
        catch (Exception ex)
        {
            ScannerSettings = new ScannerSettingsSection();
            SettingsStatus = $"Błąd wczytywania ustawień czytnika: {ex.Message}";
        }
    }

    public void SaveScannerSettings()
    {
        try
        {
            ScannerSettings.Vid = NormalizeVidPid(ScannerSettings.Vid, "VID_");
            ScannerSettings.Pid = NormalizeVidPid(ScannerSettings.Pid, "PID_");

            _databaseSettingsService.SaveScannerSettings(ScannerSettings);
            SettingsStatus = "Zapisano ustawienia czytnika. Uruchom aplikację ponownie.";
        }
        catch (Exception ex)
        {
            SettingsStatus = $"Błąd zapisu ustawień czytnika: {ex.Message}";
        }
    }

    public void LoadAdminPasswordStatus()
    {
        try
        {
            AdminPasswordExists = _adminPasswordService.HasPassword();
            AdminPasswordStatus = AdminPasswordExists
                ? "Hasło administratora jest ustawione."
                : "Hasło administratora nie jest jeszcze ustawione.";
        }
        catch (Exception ex)
        {
            AdminPasswordExists = false;
            AdminPasswordStatus = $"Błąd wczytywania hasła administratora: {ex.Message}";
        }
    }

    public bool SaveAdminPassword(string newPassword, string repeatedPassword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                AdminPasswordStatus = "Podaj nowe hasło.";
                return false;
            }

            if (newPassword.Length < 4)
            {
                AdminPasswordStatus = "Hasło musi mieć co najmniej 4 znaki.";
                return false;
            }

            if (!string.Equals(newPassword, repeatedPassword, StringComparison.Ordinal))
            {
                AdminPasswordStatus = "Hasła nie są identyczne.";
                return false;
            }

            _adminPasswordService.SavePassword(newPassword);

            AdminPasswordExists = true;
            AdminPasswordStatus = "Hasło administratora zostało zapisane.";
            return true;
        }
        catch (Exception ex)
        {
            AdminPasswordStatus = $"Błąd zapisu hasła administratora: {ex.Message}";
            return false;
        }
    }

    public bool VerifyAdminPassword(string password)
    {
        try
        {
            return _adminPasswordService.VerifyPassword(password);
        }
        catch
        {
            return false;
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
            OracleSettings.Restore(_oracleSnapshot);

        OracleSettings.IsEditing = false;
        SettingsStatus = "Anulowano zmiany Oracle.";
    }

    public void CancelEditMySql()
    {
        if (_mySqlSnapshot is not null)
            MySqlSettings.Restore(_mySqlSnapshot);

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

    public async Task LoadLoanReportsAsync()
    {
        try
        {
            LoanReports.Clear();

            var items = await _keyService.GetLoanReportAsync(null, null, null, null);

            _allLoanReports.Clear();
            _allLoanReports.AddRange(items);

            RebuildLoanReportFilterSources();
            ApplyLoanReportFilters();
        }
        catch (Exception ex)
        {
            _allLoanReports.Clear();
            LoanReports.Clear();

            ReportUsers.Clear();
            ReportKeys.Clear();
            ReportBuildings.Clear();

            ReportUsers.Add(AllUsersOption);
            ReportKeys.Add(AllKeysOption);
            ReportBuildings.Add(AllBuildingsOption);

            SelectedReportUser = AllUsersOption;
            SelectedReportKey = AllKeysOption;
            SelectedReportBuilding = AllBuildingsOption;

            ReportStatus = $"Błąd raportu: {ex.Message}";
        }
    }

    public Task ClearLoanReportFiltersAsync()
    {
        ReportDateFrom = null;
        ReportDateTo = null;
        SelectedReportUser = AllUsersOption;
        SelectedReportKey = AllKeysOption;
        SelectedReportBuilding = AllBuildingsOption;
        ApplyLoanReportFilters();
        return Task.CompletedTask;
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
            return;

        if (_firstScanAt.HasValue && DateTime.Now - _firstScanAt.Value > ScanWindow)
            ClearScannerState("Przekroczono 10 sekund. Wyczyściłem dane.");

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
                await LoadLoanReportsAsync();
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
            InventoryGroups.Clear();

            var items = await _keyService.GetKeysAsync();

            foreach (var item in items)
                Keys.Add(item);

            RebuildInventoryGroups();

            if (currentSelectedId.HasValue)
                SelectedKey = Keys.FirstOrDefault(x => x.Id == currentSelectedId.Value);
        }
        catch (Exception ex)
        {
            Keys.Clear();
            InventoryGroups.Clear();
            KeysStatus = $"Błąd MariaDB: {ex.Message}";
        }
    }

    public async Task CreateKeyAsync(string name, uint buildingId, string? hanger, string? description)
    {
        try
        {
            await _keyService.InsertAsync(name, buildingId, hanger, description);
            KeysStatus = "Dodano klucz.";
            await RefreshKeysAsync();
        }
        catch (Exception ex)
        {
            KeysStatus = $"Błąd dodawania: {ex.Message}";
        }
    }

    public async Task EditKeyAsync(uint id, string name, uint buildingId, string? hanger, string? description, bool removeRfid)
    {
        try
        {
            await _keyService.UpdateAsync(id, name, buildingId, hanger, description, removeRfid);
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

    private void RebuildInventoryGroups()
    {
        InventoryGroups.Clear();

        var groups = Keys
            .GroupBy(x => x.BuildingDisplay, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var inventoryGroup = new InventoryKeyGroup
            {
                BuildingName = group.Key
            };

            foreach (var key in group.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                inventoryGroup.Keys.Add(key);

            InventoryGroups.Add(inventoryGroup);
        }
    }

    private void RebuildLoanReportFilterSources()
    {
        var currentUser = SelectedReportUser;
        var currentKey = SelectedReportKey;
        var currentBuilding = SelectedReportBuilding;

        var users = _allLoanReports
            .Select(x => x.UserName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var keys = _allLoanReports
            .Select(x => x.KeyName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var buildings = _allLoanReports
            .Select(x => x.Building)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        ReportUsers.Clear();
        ReportUsers.Add(AllUsersOption);
        foreach (var user in users)
            ReportUsers.Add(user);

        ReportKeys.Clear();
        ReportKeys.Add(AllKeysOption);
        foreach (var key in keys)
            ReportKeys.Add(key);

        ReportBuildings.Clear();
        ReportBuildings.Add(AllBuildingsOption);
        foreach (var building in buildings)
            ReportBuildings.Add(building);

        SelectedReportUser = ReportUsers.Contains(currentUser) ? currentUser : AllUsersOption;
        SelectedReportKey = ReportKeys.Contains(currentKey) ? currentKey : AllKeysOption;
        SelectedReportBuilding = ReportBuildings.Contains(currentBuilding) ? currentBuilding : AllBuildingsOption;
    }

    private void ApplyLoanReportFilters()
    {
        IEnumerable<KeyLoanReportItem> filtered = _allLoanReports;

        if (ReportDateFrom.HasValue)
            filtered = filtered.Where(x => x.EventTime.Date >= ReportDateFrom.Value.Date);

        if (ReportDateTo.HasValue)
            filtered = filtered.Where(x => x.EventTime.Date <= ReportDateTo.Value.Date);

        if (!string.IsNullOrWhiteSpace(SelectedReportUser) &&
            !string.Equals(SelectedReportUser, AllUsersOption, StringComparison.Ordinal))
        {
            filtered = filtered.Where(x => string.Equals(x.UserName, SelectedReportUser, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedReportKey) &&
            !string.Equals(SelectedReportKey, AllKeysOption, StringComparison.Ordinal))
        {
            filtered = filtered.Where(x => string.Equals(x.KeyName, SelectedReportKey, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedReportBuilding) &&
            !string.Equals(SelectedReportBuilding, AllBuildingsOption, StringComparison.Ordinal))
        {
            filtered = filtered.Where(x => string.Equals(x.Building, SelectedReportBuilding, StringComparison.OrdinalIgnoreCase));
        }

        var items = filtered
            .OrderByDescending(x => x.EventTime)
            .ToList();

        LoanReports.Clear();
        foreach (var item in items)
            LoanReports.Add(item);
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
        CurrentKeyBuilding = key.BuildingDisplay;
        CurrentKeyDescription = key.Description ?? string.Empty;
        CurrentKeyHanger = key.Hanger ?? string.Empty;
        CurrentKeyRfidStatus = string.Empty;
    }

    private void ClearScannerPanels()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        EmployeeCardDisplay = string.Empty;

        CurrentKeyName = string.Empty;
        CurrentKeyBuilding = string.Empty;
        CurrentKeyDescription = string.Empty;
        CurrentKeyHanger = string.Empty;
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
                    return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_pendingPerson is not null || _pendingKey is not null)
                        ClearScannerState("Przekroczono 10 sekund. Wyczyściłem dane.");
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
            return;

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
                    return;

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
            return;

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

    private static string NormalizeVidPid(string value, string prefix)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
            return prefix == "VID_" ? "VID_08FF" : "PID_0009";

        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            normalized = prefix + normalized;

        return normalized;
    }

        public async Task RefreshBuildingsAsync()
    {
        try
        {
            var currentSelectedId = SelectedBuilding?.Id;

            Buildings.Clear();

            var items = await _keyService.GetBuildingsAsync();

            foreach (var item in items)
                Buildings.Add(item);

            if (currentSelectedId.HasValue)
                SelectedBuilding = Buildings.FirstOrDefault(x => x.Id == currentSelectedId.Value);
        }
        catch (Exception ex)
        {
            Buildings.Clear();
            KeysStatus = $"Błąd wczytywania budynków: {ex.Message}";
        }
    }

    public async Task CreateBuildingAsync(string name)
    {
        try
        {
            await _keyService.InsertBuildingAsync(name);
            KeysStatus = "Dodano budynek.";
            await RefreshBuildingsAsync();
        }
        catch (Exception ex)
        {
            KeysStatus = $"Błąd dodawania budynku: {ex.Message}";
        }
    }

    public async Task EditBuildingAsync(uint id, string name)
    {
        try
        {
            await _keyService.UpdateBuildingAsync(id, name);
            KeysStatus = "Zaktualizowano budynek.";
            await RefreshBuildingsAsync();
            await RefreshKeysAsync();
            SelectedBuilding = Buildings.FirstOrDefault(x => x.Id == id);
        }
        catch (Exception ex)
        {
            KeysStatus = $"Błąd edycji budynku: {ex.Message}";
        }
    }

    public async Task DeleteSelectedBuildingAsync()
    {
        if (SelectedBuilding is null)
        {
            KeysStatus = "Nie wybrano budynku.";
            return;
        }

        try
        {
            var id = SelectedBuilding.Id;
            await _keyService.DeleteBuildingAsync(id);
            KeysStatus = "Usunięto budynek.";
            SelectedBuilding = null;
            await RefreshBuildingsAsync();
            await RefreshKeysAsync();
        }
        catch (Exception ex)
        {
            KeysStatus = $"Błąd usuwania budynku: {ex.Message}";
        }
    }
}