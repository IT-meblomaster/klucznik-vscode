using System.Windows;

namespace Klucznik;

public partial class KeyEditDialog : Window
{
    public string KeyNameValue => NameTextBox.Text.Trim();
    public string KeyBuildingValue => BuildingTextBox.Text.Trim();
    public string KeyHangerValue => HangerTextBox.Text.Trim();
    public string KeyDescriptionValue => DescriptionTextBox.Text.Trim();
    public bool RemoveRfid => false;

    public KeyEditDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public void SetModeForCreate()
    {
        Title = "Nowy klucz";
    }

    public void SetModeForEdit(string name, string? building, string? hanger, string? description)
    {
        Title = "Edytuj klucz";
        NameTextBox.Text = name;
        BuildingTextBox.Text = building ?? string.Empty;
        HangerTextBox.Text = hanger ?? string.Empty;
        DescriptionTextBox.Text = description ?? string.Empty;
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
