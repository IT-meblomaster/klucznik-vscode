namespace MojaAplikacja.Models;

public class KeyItem
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Hanger { get; set; }
    public string? Description { get; set; }
    public string? RfidTag { get; set; }
    public uint? CurrentRfidTagId { get; set; }
    public bool IsActive { get; set; }
    public bool IsIssued { get; set; }

    public string? IssuedToName { get; set; }
    public DateTime? IssuedAt { get; set; }

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