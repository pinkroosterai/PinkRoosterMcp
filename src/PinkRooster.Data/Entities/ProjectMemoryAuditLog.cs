namespace PinkRooster.Data.Entities;

public sealed class ProjectMemoryAuditLog : IAuditLogEntry
{
    public long Id { get; set; }
    public long ProjectMemoryId { get; set; }
    public ProjectMemory ProjectMemory { get; set; } = null!;
    public required string FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public required string ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
