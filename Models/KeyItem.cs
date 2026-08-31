using CommunityToolkit.Mvvm.ComponentModel;

namespace Klucznik.Models;

public partial class KeyItem : ObservableObject
{
    [ObservableProperty]
    private uint id;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private uint buildingId;

    [ObservableProperty]
    private string? building;

    [ObservableProperty]
    private string? hanger;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private string? rfidTag;

    [ObservableProperty]
    private uint? currentRfidTagId;

    [ObservableProperty]
    private bool isActive;

    [NotifyPropertyChangedFor(nameof(InventoryStatusText))]
    [NotifyPropertyChangedFor(nameof(InventoryTooltip))]
    [ObservableProperty]
    private bool isIssued;

    [NotifyPropertyChangedFor(nameof(InventoryTooltip))]
    [ObservableProperty]
    private string? issuedToName;

    [NotifyPropertyChangedFor(nameof(InventoryTooltip))]
    [ObservableProperty]
    private DateTime? issuedAt;

    public string BuildingDisplay =>
        string.IsNullOrWhiteSpace(Building) ? "Bez budynku" : Building.Trim();

    public string KeyWithBuildingDisplay =>
        $"{Name} ({BuildingDisplay})";

    public string InventoryDescriptionLine =>
        string.IsNullOrWhiteSpace(Description) ? string.Empty : Description.Trim();

    public string InventoryHangerLine =>
        string.IsNullOrWhiteSpace(Hanger) ? string.Empty : $"({Hanger.Trim()})";

    public string RfidStatus =>
        string.IsNullOrWhiteSpace(RfidTag) ? "Brak RFID" : "Przypisany";

    public bool HasRfid =>
        !string.IsNullOrWhiteSpace(RfidTag);

    public string InventoryStatusText =>
        IsIssued ? "Wypożyczony" : "Dostępny";

    public string InventoryTooltip =>
        IsIssued
            ? $"Pobrał: {IssuedToName ?? "nieznany"}{Environment.NewLine}Data: {(IssuedAt.HasValue ? IssuedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "brak danych")}"
            : "Klucz dostępny do pobrania.";
}