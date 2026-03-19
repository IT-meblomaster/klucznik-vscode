using System.Windows;
using System.Windows.Input;
using MojaAplikacja.ViewModels;

namespace MojaAplikacja;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CardInputTextBox.Focus();
        Keyboard.Focus(CardInputTextBox);
    }

    private async void CardInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (DataContext is not MainViewModel vm)
            return;

        e.Handled = true;

        await vm.LookupCardCommand.ExecuteAsync(null);
        vm.ClearCardInput();

        CardInputTextBox.Focus();
        Keyboard.Focus(CardInputTextBox);
        CardInputTextBox.SelectAll();
    }

    private async void KeysTab_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.LoadKeysCommand.ExecuteAsync(null);
        }
    }

    private async void KeyRfidScanTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (DataContext is not MainViewModel vm)
            return;

        e.Handled = true;

        vm.AssignScannedRfidCommand.Execute(null);
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

        CardInputTextBox.Focus();
        Keyboard.Focus(CardInputTextBox);
    }
}