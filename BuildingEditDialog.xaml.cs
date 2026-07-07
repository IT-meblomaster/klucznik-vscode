using System.Windows;

namespace Klucznik;

public partial class BuildingEditDialog : Window
{
    public string BuildingNameValue => NameTextBox.Text.Trim();

    public BuildingEditDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public void SetModeForCreate()
    {
        Title = "Nowy budynek";
    }

    public void SetModeForEdit(string name)
    {
        Title = "Edytuj budynek";
        NameTextBox.Text = name;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BuildingNameValue))
        {
            MessageBox.Show("Nazwa budynku jest wymagana.");
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