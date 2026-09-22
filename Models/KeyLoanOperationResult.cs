namespace Klucznik.Models;

public class KeyLoanOperationResult
{
    public bool IsRestricted { get; set; }
    public bool HasConflict { get; set; }
    public bool IsIssue { get; set; }
    public bool IsReturn { get; set; }
    public string Message { get; set; } = string.Empty;
}

