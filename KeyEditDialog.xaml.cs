using System.Windows;
using Klucznik.Models;

namespace Klucznik;

public partial class KeyEditDialog : Window
{
    public string KeyNameValue => NameTextBox.Text.Trim();
    public uint KeyBuildingIdValue => BuildingComboBox.SelectedValue is uint value ? value : 0;
    public string KeyHangerValue => HangerTextBox.Text.Trim();
    public string KeyDescriptionValue => DescriptionTextBox.Text.Trim();
    public bool RemoveRfid => false;

    public KeyEditDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public void SetBuildings(IEnumerable<BuildingItem> buildings)
    {
        BuildingComboBox.ItemsSource = buildings.ToList();
    }

    public void SetModeForCreate()
    {
        Title = "Nowy klucz";
    }

    public void SetModeForEdit(string name, uint buildingId, string? hanger, string? description)
    {
        Title = "Edytuj klucz";
        NameTextBox.Text = name;
        BuildingComboBox.SelectedValue = buildingId;
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

        if (KeyBuildingIdValue == 0)
        {
            MessageBox.Show("Wybierz budynek.");
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