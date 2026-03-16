using PinkRooster.Shared.DTOs.Requests;

namespace PinkRooster.Shared.DTOs.Responses;

public sealed class WorkPackageResponse
{
    public required string WorkPackageId { get; init; }
    public long Id { get; init; }
    public int WorkPackageNumber { get; init; }
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
    public List<string> LinkedIssueIds { get; init; } = [];
    public List<string> LinkedFeatureRequestIds { get; init; } = [];
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public List<FileReferenceDto> Attachments { get; init; } = [];
    public List<PhaseResponse> Phases { get; init; } = [];
    public List<DependencyResponse> BlockedBy { get; init; } = [];
    public List<DependencyResponse> Blocking { get; init; } = [];
    public int TaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public List<StateChangeDto>? StateChanges { get; set; }
}
