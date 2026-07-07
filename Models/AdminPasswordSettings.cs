namespace Klucznik.Models;

public class AdminPasswordSettings
{
    public string Algorithm { get; set; } = "PBKDF2-SHA256";
    public int Iterations { get; set; } = 210_000;
    public string Salt { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}
