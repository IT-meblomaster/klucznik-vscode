using System.Collections.ObjectModel;

namespace MojaAplikacja.Models;

public class InventoryKeyGroup
{
    public string BuildingName { get; set; } = string.Empty;
    public ObservableCollection<KeyItem> Keys { get; } = new();
}