using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MojaAplikacja.Models;
using MojaAplikacja.Services;
using MySqlConnector;
using System.Collections.ObjectModel;

namespace MojaAplikacja.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly OracleTestService _oracleService = new();
    private readonly KeyService _keyService = new();

    [ObservableProperty]
    private string cardNumber = string.Empty;

    [ObservableProperty]
    private string firstName = string.Empty;

    [ObservableProperty]
    private string lastName = string.Empty;

    [ObservableProperty]
    private string status = "Przyłóż kartę do czytnika.";

    [ObservableProperty]
    private string keysStatus = "Gotowe.";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private KeyItem? selectedKey;

    [ObservableProperty]
    private string keyName = string.Empty;

    [ObservableProperty]
    private string keyDescription = string.Empty;

    [ObservableProperty]
    private string keyRfidTag = string.Empty;

    [ObservableProperty]
    private bool keyIsActive = true;

    [ObservableProperty]
    private string keySearchText = string.Empty;

    [ObservableProperty]
    private bool keyOnlyActive = true;

    [ObservableProperty]
    private bool keyOnlyWithoutRfid;

    [ObservableProperty]
    private string scannedKeyRfid = string.Empty;

    public ObservableCollection<KeyItem> Keys { get; } = new();

    partial void OnSelectedKeyChanged(KeyItem? value)
    {
        if (value is null)
        {
            KeyName = string.Empty;
            KeyDescription = string.Empty;
            KeyRfidTag = string.Empty;
            KeyIsActive = true;
            ScannedKeyRfid = string.Empty;
            return;
        }

        KeyName = value.Name;
        KeyDescription = value.Description ?? string.Empty;
        KeyRfidTag = value.RfidTag ?? string.Empty;
        KeyIsActive = value.IsActive;
        ScannedKeyRfid = string.Empty;
    }

    [RelayCommand]
    private async Task LookupCardAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var scannedValue = CardNumber?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(scannedValue))
        {
            Status = "Brak numeru karty.";
            return;
        }

        try
        {
            IsBusy = true;
            Status = "Szukanie osoby...";

            var person = await _oracleService.FindPersonByCardAsync(scannedValue);

            if (person is null)
            {
                FirstName = string.Empty;
                LastName = string.Empty;
                Status = $"Nie znaleziono osoby dla numeru: {scannedValue}";
            }
            else
            {
                FirstName = person.FirstName;
                LastName = person.LastName;
                Status = $"Znaleziono: {person.FirstName} {person.LastName}";
            }
        }
        catch (Exception ex)
        {
            FirstName = string.Empty;
            LastName = string.Empty;
            Status = $"Błąd: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadKeysAsync()
    {
        try
        {
            Keys.Clear();
            KeysStatus = "Ładowanie kluczy...";

            var items = await _keyService.GetKeysAsync(KeySearchText, KeyOnlyActive, KeyOnlyWithoutRfid);

            foreach (var item in items)
            {
                Keys.Add(item);
            }

            KeysStatus = $"Wczytano {Keys.Count} kluczy.";
        }
        catch (Exception ex)
        {
            Keys.Clear();
            KeysStatus = $"Błąd MariaDB: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ApplyKeyFiltersAsync()
    {
        await LoadKeysAsync();
    }

    [RelayCommand]
    private void NewKey()
    {
        SelectedKey = null;
        KeyName = string.Empty;
        KeyDescription = string.Empty;
        KeyRfidTag = string.Empty;
        KeyIsActive = true;
        ScannedKeyRfid = string.Empty;
        KeysStatus = "Nowy rekord.";
    }

    [RelayCommand]
    private async Task SaveKeyAsync()
    {
        try
        {
            var normalizedName = KeyName.Trim();

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                KeysStatus = "Nazwa jest wymagana.";
                return;
            }

            var item = new KeyItem
            {
                Id = SelectedKey?.Id ?? 0,
                Name = normalizedName,
                Description = string.IsNullOrWhiteSpace(KeyDescription) ? null : KeyDescription.Trim(),
                RfidTag = string.IsNullOrWhiteSpace(KeyRfidTag) ? null : KeyRfidTag.Trim(),
                IsActive = KeyIsActive
            };

            if (SelectedKey is null)
            {
                var newId = await _keyService.InsertAsync(item);
                KeysStatus = $"Dodano klucz (ID: {newId}).";
            }
            else
            {
                await _keyService.UpdateAsync(item);
                KeysStatus = "Zaktualizowano klucz.";
            }

            await LoadKeysAsync();
            ClearKeyFormInternal();
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            KeysStatus = "Duplikat danych. Nazwa lub RFID już istnieje.";
        }
        catch (Exception ex)
        {
            KeysStatus = $"Błąd zapisu: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteKeyAsync()
    {
        try
        {
            if (SelectedKey is null)
            {
                KeysStatus = "Brak wybranego rekordu.";
                return;
            }

            await _keyService.SoftDeleteAsync(SelectedKey.Id);
            KeysStatus = "Klucz dezaktywowany.";

            await LoadKeysAsync();
            ClearKeyFormInternal();
        }
        catch (Exception ex)
        {
            KeysStatus = $"Błąd usuwania: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearKeyForm()
    {
        ClearKeyFormInternal();
        KeysStatus = "Wyczyszczono formularz.";
    }

    [RelayCommand]
    private void AssignScannedRfid()
    {
        var value = ScannedKeyRfid.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            KeysStatus = "Brak zeskanowanego RFID.";
            return;
        }

        KeyRfidTag = value;
        KeysStatus = $"Przepisano RFID: {value}";
    }

    private void ClearKeyFormInternal()
    {
        SelectedKey = null;
        KeyName = string.Empty;
        KeyDescription = string.Empty;
        KeyRfidTag = string.Empty;
        KeyIsActive = true;
        ScannedKeyRfid = string.Empty;
    }

    public void ClearCardInput()
    {
        CardNumber = string.Empty;
    }

    public void ClearScannedKeyRfid()
    {
        ScannedKeyRfid = string.Empty;
    }
}