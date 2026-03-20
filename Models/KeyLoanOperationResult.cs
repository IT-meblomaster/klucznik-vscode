namespace MojaAplikacja.Models;

public class KeyLoanOperationResult
{
    public bool IsIssue { get; set; }
    public bool IsReturn { get; set; }
    public string Message { get; set; } = string.Empty;
}