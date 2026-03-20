using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MojaAplikacja.ViewModels;

namespace MojaAplikacja;

public partial class MainWindow : Window
{
    private bool _keysLoadedOnce = false;

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

        CardInputTextBox.Focus();
        Keyboard.Focus(CardInputTextBox);
    }

    private void SyncPasswordBoxesFromViewModel()
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (OraclePasswordBox.Password != vm.OracleSettings.Password)
            OraclePasswordBox.Password = vm.OracleSettings.Password ?? string.Empty;

        if (MySqlPasswordBox.Password != vm.MySqlSettings.Password)
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

    private async void CardInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (DataContext is not MainViewModel vm)
            return;

        e.Handled = true;

        var code = CardInputTextBox.Text.Trim();
        CardInputTextBox.Clear();

        await vm.ProcessScannerCodeAsync(code);

        CardInputTextBox.Focus();
        Keyboard.Focus(CardInputTextBox);
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

        dialog.SetModeForEdit(key.Name, key.Description, key.HasRfid);

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
        MessageBox.Show("Blokada aplikacji - do implementacji.");
    }
}