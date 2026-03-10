using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class CreateIssueRequest
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required IssueType IssueType { get; init; }
    public required IssueSeverity Severity { get; init; }
    public Priority Priority { get; init; } = Priority.Medium;
    public string? StepsToReproduce { get; init; }
    public string? ExpectedBehavior { get; init; }
    public string? ActualBehavior { get; init; }
    public string? AffectedComponent { get; init; }
    public string? StackTrace { get; init; }
    public string? RootCause { get; init; }
    public string? Resolution { get; init; }
    public CompletionState State { get; init; } = CompletionState.NotStarted;
    public List<FileReferenceDto>? Attachments { get; init; }
}
