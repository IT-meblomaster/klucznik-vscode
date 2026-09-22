namespace Klucznik.Models;

public sealed class KeyAccessPolicy
{
    public uint KeyId { get; init; }
    public bool IsRestricted { get; init; }
    public HashSet<string> Cards { get; } = new(StringComparer.Ordinal);
}

public sealed class KeyAccessSnapshot
{
    public DateTime SyncedAtUtc { get; init; } = DateTime.UtcNow;
    public List<KeyAccessPolicy> Policies { get; } = new();
}

public sealed class KeyAccessDeniedException : InvalidOperationException
{
    public KeyAccessDeniedException(string message) : base(message) { }
}

public static class CardNumber
{
    // Tak samo jak dotychczasowy odczyt users_saik: zera wiodące są pomijane.
    // Nie konwertujemy do liczby (karty mogą mieć do 64 cyfr).
    public static string Normalize(string value)
    {
        var card = value.Trim();
        if (card.Length == 0 || card.Length > 64 || card.Any(c => c < '0' || c > '9'))
            throw new KeyAccessDeniedException("Nieprawidłowy numer karty (wymagane 1–64 cyfry).");
        var normalized = card.TrimStart('0');
        return normalized.Length == 0 ? "0" : normalized;
    }
}
