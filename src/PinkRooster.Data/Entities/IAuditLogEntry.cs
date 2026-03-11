namespace PinkRooster.Data.Entities;

public interface IAuditLogEntry
{
    string FieldName { get; set; }
    string? OldValue { get; set; }
    string? NewValue { get; set; }
}
