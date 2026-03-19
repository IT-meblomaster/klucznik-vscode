namespace MojaAplikacja.Models;

public class KeyItem
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RfidTag { get; set; }
    public bool IsActive { get; set; }

    public string RfidStatus => string.IsNullOrWhiteSpace(RfidTag) ? "Brak RFID" : "Przypisany";
}