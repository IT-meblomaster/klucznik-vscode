using System.Windows;
using System.Windows.Input;

namespace MojaAplikacja;

public partial class RfidAssignDialog : Window
{
    public string RfidValue { get; private set; } = string.Empty;

    public RfidAssignDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            HiddenRfidTextBox.Focus();
            Keyboard.Focus(HiddenRfidTextBox);
        };
    }

    private void HiddenRfidTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        var value = HiddenRfidTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(value))
            return;

        RfidValue = value;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}