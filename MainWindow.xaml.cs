using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Klucznik.Services;
using Klucznik.ViewModels;

namespace Klucznik;

public partial class MainWindow : Window
{
    private bool _keysLoadedOnce = false;
    private bool _reportsLoadedOnce = false;
    private bool _adminUnlocked = false;
    private bool _isChangingTabProgrammatically = false;

    private readonly StringBuilder _scanBuffer = new();
    private DateTime _lastScanCharAt = DateTime.MinValue;
    private static readonly TimeSpan ScanGapReset = TimeSpan.FromMilliseconds(250);

    private RawInputRfidScanner? _rawInputScanner;

    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainViewModel();
        DataContext = vm;

        CreateRawInputScanner(vm.ScannerSettings.Vid, vm.ScannerSettings.Pid);

        Loaded += MainWindow_Loaded;
        MainTabs.SelectionChanged += MainTabs_SelectionChanged;
        Closed += MainWindow_Closed;
    }

    private void CreateRawInputScanner(string vid, string pid)
    {
        _rawInputScanner?.CodeScanned -= RawInputScanner_CodeScanned;
        _rawInputScanner?.Dispose();

        _rawInputScanner = new RawInputRfidScanner(this, vid, pid);
        _rawInputScanner.CodeScanned += RawInputScanner_CodeScanned;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _rawInputScanner?.CodeScanned -= RawInputScanner_CodeScanned;
        _rawInputScanner?.Dispose();
        _rawInputScanner = null;
    }

    private async void RawInputScanner_CodeScanned(object? sender, string code)
    {
        if (DataContext is not MainViewModel vm)
            return;

        await vm.ProcessScannerCodeAsync(code);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SyncPasswordBoxesFromViewModel();

        if (DataContext is MainViewModel vm && !vm.AdminPasswordExists)
        {
            SetAdminUnlocked(true);
            SelectTabProgrammatically(4);
            vm.AdminPasswordStatus = "Ustaw hasło administratora przed dalszą konfiguracją.";
            Keyboard.Focus(AdminNewPasswordBox);
            return;
        }

        SetAdminUnlocked(false);
        Keyboard.Focus(this);
    }

    private void SetAdminUnlocked(bool unlocked)
    {
        _adminUnlocked = unlocked;
        LockButton.Content = unlocked ? "Zablokuj ustawienia" : "Odblokuj ustawienia";
    }

    private void SelectTabProgrammatically(int tabIndex)
    {
        _isChangingTabProgrammatically = true;
        MainTabs.SelectedIndex = tabIndex;
        _isChangingTabProgrammatically = false;
    }

    private bool EnsureAdminUnlocked()
    {
        if (_adminUnlocked)
            return true;

        if (DataContext is not MainViewModel vm)
            return false;

        if (!vm.AdminPasswordExists)
        {
            SetAdminUnlocked(true);
            return true;
        }

        string errorMessage = string.Empty;

        while (true)
        {
            var dialog = new AdminPasswordDialog { Owner = this };

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                dialog.Loaded += (_, _) => dialog.SetError(errorMessage);
            }

            if (dialog.ShowDialog() != true)
                return false;

            if (string.IsNullOrWhiteSpace(dialog.Password))
            {
                errorMessage = "Podaj hasło.";
                continue;
            }

            if (vm.VerifyAdminPassword(dialog.Password))
            {
                SetAdminUnlocked(true);
                return true;
            }

            errorMessage = "Nieprawidłowe hasło.";
        }
    }

    private void ProtectedTab_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_adminUnlocked)
            return;

        e.Handled = true;

        if (sender is not TabItem tab)
            return;

        if (EnsureAdminUnlocked())
        {
            tab.IsSelected = true;
        }
    }

    private void SyncPasswordBoxesFromViewModel()
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (OraclePasswordBox is not null && OraclePasswordBox.Password != vm.OracleSettings.Password)
            OraclePasswordBox.Password = vm.OracleSettings.Password ?? string.Empty;

        if (MySqlPasswordBox is not null && MySqlPasswordBox.Password != vm.MySqlSettings.Password)
            MySqlPasswordBox.Password = vm.MySqlSettings.Password ?? string.Empty;
    }

    private async void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isChangingTabProgrammatically)
            return;

        if (MainTabs.SelectedItem is not TabItem selectedTab)
            return;

        var header = selectedTab.Header?.ToString();

        if ((header == "Klucze" || header == "Budynki" || header == "Ustawienia") && !_adminUnlocked)
        {
            SelectTabProgrammatically(0);
            return;
        }


        if (header == "Budynki")
        {
            if (DataContext is MainViewModel vm)
                await vm.RefreshBuildingsAsync();

            return;
        }



        if (header == "Klucze" || header == "Inwentaryzacja")
        {
            if (_keysLoadedOnce)
                return;

            if (DataContext is MainViewModel vm)
            {
                _keysLoadedOnce = true;
                await vm.RefreshKeysAsync();
            }

            return;
        }

        if (header == "Logi")
        {
            if (_reportsLoadedOnce)
                return;

            if (DataContext is MainViewModel vm)
            {
                _reportsLoadedOnce = true;
                await vm.LoadLoanReportsAsync();
            }

            return;
        }

        if (header == "Ustawienia")
        {
            SyncPasswordBoxesFromViewModel();
        }
    }

    private bool IsEditingTextInput()
    {
        var focused = Keyboard.FocusedElement;

        return focused is TextBox
            || focused is PasswordBox
            || focused is ComboBox;
    }

    private void ResetScanBuffer()
    {
        _scanBuffer.Clear();
        _lastScanCharAt = DateTime.MinValue;
    }

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_rawInputScanner is not null && _rawInputScanner.IsRegistered)
            return;

        if (IsEditingTextInput())
            return;

        if (string.IsNullOrEmpty(e.Text))
            return;

        var now = DateTime.Now;

        if (_lastScanCharAt != DateTime.MinValue && now - _lastScanCharAt > ScanGapReset)
        {
            _scanBuffer.Clear();
        }

        _lastScanCharAt = now;
        _scanBuffer.Append(e.Text);
        e.Handled = true;
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_rawInputScanner is not null && _rawInputScanner.IsRegistered)
            return;

        if (IsEditingTextInput())
            return;

        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            if (DataContext is not MainViewModel vm)
                return;

            var code = _scanBuffer.ToString().Trim();
            ResetScanBuffer();

            if (string.IsNullOrWhiteSpace(code))
                return;

            e.Handled = true;
            await vm.ProcessScannerCodeAsync(code);
            return;
        }

        if (e.Key == Key.Escape)
        {
            ResetScanBuffer();
            return;
        }

        if (e.Key == Key.Back && _scanBuffer.Length > 0)
        {
            _scanBuffer.Length -= 1;
            e.Handled = true;
        }
    }

    private async void NewKey_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var buildings = await vm.GetBuildingsAsync();

        if (buildings.Count == 0)
        {
            MessageBox.Show("Brak aktywnych budynków w bazie.");
            return;
        }

        var dialog = new KeyEditDialog { Owner = this };
        dialog.SetBuildings(buildings);
        dialog.SetModeForCreate();

        if (dialog.ShowDialog() == true)
        {
            await vm.CreateKeyAsync(
                dialog.KeyNameValue,
                dialog.KeyBuildingIdValue,
                dialog.KeyHangerValue,
                dialog.KeyDescriptionValue);
        }
    }

    private async void EditKey_Click(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedKey is null)
            return;

        var key = vm.SelectedKey;
        var buildings = await vm.GetBuildingsAsync();

        if (buildings.Count == 0)
        {
            MessageBox.Show("Brak aktywnych budynków w bazie.");
            return;
        }

        var dialog = new KeyEditDialog { Owner = this };
        dialog.SetBuildings(buildings);
        dialog.SetModeForEdit(key.Name, key.BuildingId, key.Hanger, key.Description);

        if (dialog.ShowDialog() == true)
        {
            await vm.EditKeyAsync(
                key.Id,
                dialog.KeyNameValue,
                dialog.KeyBuildingIdValue,
                dialog.KeyHangerValue,
                dialog.KeyDescriptionValue,
                dialog.RemoveRfid);
        }
    }

    private async void DeleteKey_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedKey is null)
            return;

        var result = MessageBox.Show(
            $"Usunąć klucz \"{vm.SelectedKey.Name}\"?",
            "Potwierdzenie",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await vm.DeleteSelectedKeyAsync();
    }

    private async void AssignRfid_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedKey is null)
            return;

        var dialog = new RfidAssignDialog { Owner = this };

        if (dialog.ShowDialog() == true)
        {
            await vm.AssignRfidToSelectedKeyAsync(dialog.RfidValue);

            if (vm.KeysStatus.StartsWith("RFID przypisany do klucza"))
            {
                MessageBox.Show(vm.KeysStatus, "RFID zajęty", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private async void RemoveRfid_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedKey is null)
            return;

        var result = MessageBox.Show(
            $"Usunąć RFID z klucza \"{vm.SelectedKey.Name}\"?",
            "Potwierdzenie",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await vm.RemoveRfidFromSelectedKeyAsync();
    }

    private async void ClearReportFilters_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            await vm.ClearLoanReportFiltersAsync();
    }

    private void KeysGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedKey is null)
            return;

        EditKey_Click(sender, e);
    }

    private void EditOracle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.BeginEditOracle();
            SyncPasswordBoxesFromViewModel();
        }
    }

    private void SaveOracle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OracleSettings.Password = OraclePasswordBox.Password;
            vm.SaveOracle();
            SyncPasswordBoxesFromViewModel();
        }
    }

    private void CancelOracle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CancelEditOracle();
            SyncPasswordBoxesFromViewModel();
        }
    }

    private void EditMySql_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.BeginEditMySql();
            SyncPasswordBoxesFromViewModel();
        }
    }

    private void SaveMySql_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.MySqlSettings.Password = MySqlPasswordBox.Password;
            vm.SaveMySql();
            SyncPasswordBoxesFromViewModel();
        }
    }

    private void CancelMySql_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CancelEditMySql();
            SyncPasswordBoxesFromViewModel();
        }
    }

    private void SaveScannerSettings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        vm.SaveScannerSettings();
        CreateRawInputScanner(vm.ScannerSettings.Vid, vm.ScannerSettings.Pid);
        Keyboard.Focus(this);
    }

    private void OraclePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OracleSettings.Password = OraclePasswordBox.Password;
    }

    private void MySqlPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.MySqlSettings.Password = MySqlPasswordBox.Password;
    }

    private void AdminNewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel)
            return;
    }

    private void AdminRepeatPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel)
            return;
    }

    private void SaveAdminPassword_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (vm.SaveAdminPassword(AdminNewPasswordBox.Password, AdminRepeatPasswordBox.Password))
        {
            AdminNewPasswordBox.Clear();
            AdminRepeatPasswordBox.Clear();
            SetAdminUnlocked(true);
        }
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        if (_adminUnlocked)
        {
            SetAdminUnlocked(false);
            SelectTabProgrammatically(0);
            return;
        }

        EnsureAdminUnlocked();
    }


    private async void NewBuilding_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var dialog = new BuildingEditDialog { Owner = this };
        dialog.SetModeForCreate();

        if (dialog.ShowDialog() == true)
            await vm.CreateBuildingAsync(dialog.BuildingNameValue);
    }

    private async void EditBuilding_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedBuilding is null)
            return;

        var building = vm.SelectedBuilding;

        var dialog = new BuildingEditDialog { Owner = this };
        dialog.SetModeForEdit(building.Name);

        if (dialog.ShowDialog() == true)
            await vm.EditBuildingAsync(building.Id, dialog.BuildingNameValue);
    }

    private async void DeleteBuilding_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedBuilding is null)
            return;

        var result = MessageBox.Show(
            $"Usunąć budynek \"{vm.SelectedBuilding.Name}\"?",
            "Potwierdzenie",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await vm.DeleteSelectedBuildingAsync();
    }

    private void BuildingsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedBuilding is null)
            return;

        EditBuilding_Click(sender, e);
    }



}