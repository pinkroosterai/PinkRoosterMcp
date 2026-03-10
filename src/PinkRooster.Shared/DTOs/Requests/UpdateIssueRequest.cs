using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class UpdateIssueRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public IssueType? IssueType { get; init; }
    public IssueSeverity? Severity { get; init; }
    public Priority? Priority { get; init; }
    public string? StepsToReproduce { get; init; }
    public string? ExpectedBehavior { get; init; }
    public string? ActualBehavior { get; init; }
    public string? AffectedComponent { get; init; }
    public string? StackTrace { get; init; }
    public string? RootCause { get; init; }
    public string? Resolution { get; init; }
    public CompletionState? State { get; init; }
    public List<FileReferenceDto>? Attachments { get; init; }
}
