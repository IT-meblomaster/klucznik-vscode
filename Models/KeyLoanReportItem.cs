namespace Klucznik.Models;

public class KeyLoanReportItem
{
    public DateTime EventTime { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string KeyName { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserCard { get; set; } = string.Empty;
    public string? RfidCode { get; set; }
}
