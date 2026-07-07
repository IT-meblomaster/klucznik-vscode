using System.Windows;
using System.Windows.Input;

namespace Klucznik;

public partial class AdminPasswordDialog : Window
{
    public string Password => PasswordBox.Password;

    public AdminPasswordDialog()
    {
        InitializeComponent();
        Loaded += AdminPasswordDialog_Loaded;
    }

    public void SetError(string message)
    {
        ErrorTextBlock.Text = message;
        PasswordBox.Clear();
        PasswordBox.Focus();
    }

    private void AdminPasswordDialog_Loaded(object sender, RoutedEventArgs e)
    {
        PasswordBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
        }
    }
}
