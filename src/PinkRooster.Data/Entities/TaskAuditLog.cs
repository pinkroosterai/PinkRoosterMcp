namespace PinkRooster.Data.Entities;

public sealed class TaskAuditLog : IAuditLogEntry
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public WorkPackageTask Task { get; set; } = null!;
    public required string FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public required string ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
