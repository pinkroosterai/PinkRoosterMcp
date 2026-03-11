using PinkRooster.Shared.DTOs.Requests;

namespace PinkRooster.Mcp.Responses;

public sealed class WorkPackageDetailResponse
{
    public required string WorkPackageId { get; init; }
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Type { get; init; }
    public required string Priority { get; init; }
    public string? Plan { get; init; }
    public int? EstimatedComplexity { get; init; }
    public string? EstimationRationale { get; init; }
    public required string State { get; init; }
    public string? PreviousActiveState { get; init; }
    public List<string>? LinkedIssueIds { get; init; }
    public List<string>? LinkedFeatureRequestIds { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public List<FileReferenceDto>? Attachments { get; init; }
    public required List<PhaseDetailItem> Phases { get; init; }
    public List<DependencyItem>? BlockedBy { get; init; }
    public List<DependencyItem>? Blocking { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class PhaseDetailItem
{
    public required string PhaseId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public required string State { get; init; }
    public List<AcceptanceCriterionItem>? AcceptanceCriteria { get; init; }
    public required List<TaskDetailItem> Tasks { get; init; }
}

public sealed class TaskDetailItem
{
    public required string TaskId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public int SortOrder { get; init; }
    public string? ImplementationNotes { get; init; }
    public required string State { get; init; }
    public string? PreviousActiveState { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public List<FileReferenceDto>? TargetFiles { get; init; }
    public List<FileReferenceDto>? Attachments { get; init; }
    public List<DependencyItem>? BlockedBy { get; init; }
    public List<DependencyItem>? Blocking { get; init; }
}

public sealed class AcceptanceCriterionItem
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string VerificationMethod { get; init; }
    public string? VerificationResult { get; init; }
    public DateTimeOffset? VerifiedAt { get; init; }
}

public sealed class DependencyItem
{
    public required string EntityId { get; init; }
    public required string Name { get; init; }
    public required string State { get; init; }
    public string? Reason { get; init; }
}
