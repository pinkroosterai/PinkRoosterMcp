using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Mcp.Responses;

public sealed class IssueDetailResponse
{
    public required string IssueId { get; init; }
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string IssueType { get; init; }
    public required string Severity { get; init; }
    public required string Priority { get; init; }
    public required string State { get; init; }
    public string? StepsToReproduce { get; init; }
    public string? ExpectedBehavior { get; init; }
    public string? ActualBehavior { get; init; }
    public string? AffectedComponent { get; init; }
    public string? StackTrace { get; init; }
    public string? RootCause { get; init; }
    public string? Resolution { get; init; }
    public required List<FileReferenceDto> Attachments { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public List<LinkedWorkPackageItem>? LinkedWorkPackages { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
