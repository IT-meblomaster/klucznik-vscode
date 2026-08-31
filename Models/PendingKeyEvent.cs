using System;

namespace Klucznik.Models;

public class PendingKeyEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public uint KeyId { get; set; }
    public string KeyName { get; set; } = string.Empty;
    public string? KeyBuilding { get; set; }
    public uint? RfidTagId { get; set; }
    public string Action { get; set; } = string.Empty; // "ISSUE" albo "RETURN"
    public string PersonCard { get; set; } = string.Empty;
    public string PersonFirstName { get; set; } = string.Empty;
    public string PersonLastName { get; set; } = string.Empty;
    public bool PersonOffline { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int RetryCount { get; set; }
    public bool SyncConflict { get; set; }
}