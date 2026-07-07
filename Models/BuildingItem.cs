namespace Klucznik.Models;

public class BuildingItem
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}