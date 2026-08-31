namespace Klucznik.Models;

public class PersonResult
{
    public string CardNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsOfflinePlaceholder { get; set; }
}
