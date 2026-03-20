namespace MojaAplikacja.Models;

public class KeyItem
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RfidTag { get; set; }
    public bool IsActive { get; set; }
    public bool IsIssued { get; set; }

    public string? IssuedToName { get; set; }
    public DateTime? IssuedAt { get; set; }

    public string RfidStatus => string.IsNullOrWhiteSpace(RfidTag) ? "Brak RFID" : "Przypisany";
    public bool HasRfid => !string.IsNullOrWhiteSpace(RfidTag);

    public string InventoryStatusText => IsIssued ? "Wypożyczony" : "Dostępny";

    public string InventoryTooltip =>
        IsIssued
            ? $"Pobrał: {IssuedToName ?? "nieznany"}{Environment.NewLine}Data: {(IssuedAt.HasValue ? IssuedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "brak danych")}"
            : "Klucz dostępny do pobrania.";
}