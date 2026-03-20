using System.Windows;

namespace MojaAplikacja;

public partial class KeyEditDialog : Window
{
    public string KeyNameValue => NameTextBox.Text.Trim();
    public string KeyDescriptionValue => DescriptionTextBox.Text.Trim();
    public bool RemoveRfid => RemoveRfidCheckBox.IsChecked == true;

    public KeyEditDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public void SetModeForCreate()
    {
        Title = "Nowy klucz";
        RemoveRfidCheckBox.Visibility = Visibility.Collapsed;
    }

    public void SetModeForEdit(string name, string? description, bool hasRfid)
    {
        Title = "Edytuj klucz";
        NameTextBox.Text = name;
        DescriptionTextBox.Text = description ?? string.Empty;
        RemoveRfidCheckBox.Visibility = hasRfid ? Visibility.Visible : Visibility.Collapsed;
        RemoveRfidCheckBox.IsChecked = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(KeyNameValue))
        {
            MessageBox.Show("Nazwa klucza jest wymagana.");
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}