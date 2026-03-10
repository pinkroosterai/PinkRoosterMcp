namespace PinkRooster.Shared.DTOs.Responses;

public sealed class IssueAuditLogResponse
{
    public required string FieldName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public required string ChangedBy { get; init; }
    public required DateTimeOffset ChangedAt { get; init; }
}
