using CommunityToolkit.Mvvm.ComponentModel;

namespace Klucznik.Models;

public partial class DbSettingsSection : ObservableObject
{
    [ObservableProperty]
    private string sectionName = string.Empty;

    [ObservableProperty]
    private string configKey = string.Empty;

    [ObservableProperty]
    private string address = string.Empty;

    [ObservableProperty]
    private int port;

    [ObservableProperty]
    private string user = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string databaseName = string.Empty;

    [ObservableProperty]
    private bool isEditing;

    public bool IsReadOnly => !IsEditing;
    public bool ShowEditButton => !IsEditing;
    public bool ShowSaveCancelButtons => IsEditing;

    public DbSettingsSectionSnapshot CreateSnapshot()
        => new(Address, Port, User, Password, DatabaseName);

    public void Restore(DbSettingsSectionSnapshot snapshot)
    {
        Address = snapshot.Address;
        Port = snapshot.Port;
        User = snapshot.User;
        Password = snapshot.Password;
        DatabaseName = snapshot.DatabaseName;
    }

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(ShowEditButton));
        OnPropertyChanged(nameof(ShowSaveCancelButtons));
    }
}

public record DbSettingsSectionSnapshot(
    string Address,
    int Port,
    string User,
    string Password,
    string DatabaseName);
