using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MojaAplikacja.ViewModels;

namespace MojaAplikacja;

public partial class MainWindow : Window
{
    private bool _keysLoadedOnce = false;

    private readonly StringBuilder _scanBuffer = new();
    private DateTime _lastScanCharAt = DateTime.MinValue;
    private static readonly TimeSpan ScanGapReset = TimeSpan.FromMilliseconds(250);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Loaded += MainWindow_Loaded;
        MainTabs.SelectionChanged += MainTabs_SelectionChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SyncPasswordBoxesFromViewModel();
        Keyboard.Focus(this);
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
        if (MainTabs.SelectedItem is not TabItem selectedTab)
            return;

        var header = selectedTab.Header?.ToString();

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

        var dialog = new KeyEditDialog
        {
            Owner = this
        };

        dialog.SetModeForCreate();

        if (dialog.ShowDialog() == true)
        {
            await vm.CreateKeyAsync(dialog.KeyNameValue, dialog.KeyDescriptionValue);
        }
    }

    private async void EditKey_Click(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedKey is null)
            return;

        var key = vm.SelectedKey;

        var dialog = new KeyEditDialog
        {
            Owner = this
        };

        dialog.SetModeForEdit(key.Name, key.Description);

        if (dialog.ShowDialog() == true)
        {
            await vm.EditKeyAsync(key.Id, dialog.KeyNameValue, dialog.KeyDescriptionValue, dialog.RemoveRfid);
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
        {
            await vm.DeleteSelectedKeyAsync();
        }
    }

    private async void AssignRfid_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedKey is null)
            return;

        var dialog = new RfidAssignDialog
        {
            Owner = this
        };

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
        {
            await vm.RemoveRfidFromSelectedKeyAsync();
        }
    }

    private void KeysGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedKey is null)
            return;

        EditKey_Click(sender, e);
    }

    private void EditOracle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        vm.BeginEditOracle();
        SyncPasswordBoxesFromViewModel();
    }

    private void SaveOracle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        vm.OracleSettings.Password = OraclePasswordBox.Password;
        vm.SaveOracle();
        SyncPasswordBoxesFromViewModel();
    }

    private void CancelOracle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        vm.CancelEditOracle();
        SyncPasswordBoxesFromViewModel();
    }

    private void EditMySql_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        vm.BeginEditMySql();
        SyncPasswordBoxesFromViewModel();
    }

    private void SaveMySql_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        vm.MySqlSettings.Password = MySqlPasswordBox.Password;
        vm.SaveMySql();
        SyncPasswordBoxesFromViewModel();
    }

    private void CancelMySql_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        vm.CancelEditMySql();
        SyncPasswordBoxesFromViewModel();
    }

    private void OraclePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OracleSettings.Password = OraclePasswordBox.Password;
        }
    }

    private void MySqlPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.MySqlSettings.Password = MySqlPasswordBox.Password;
        }
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 0;

        if (DataContext is MainViewModel vm)
        {
            vm.FirstName = string.Empty;
            vm.LastName = string.Empty;
            vm.Status = "Zablokowano. Przyłóż kartę.";
        }

        ResetScanBuffer();
        Keyboard.Focus(this);
    }
}