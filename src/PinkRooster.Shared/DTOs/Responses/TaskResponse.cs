using PinkRooster.Shared.DTOs.Requests;

namespace PinkRooster.Shared.DTOs.Responses;

public sealed class TaskResponse
{
    public required string TaskId { get; init; }
    public long Id { get; init; }
    public int TaskNumber { get; init; }
    public required string PhaseId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public int SortOrder { get; init; }
    public string? ImplementationNotes { get; init; }
    public required string State { get; init; }
    public string? PreviousActiveState { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public List<FileReferenceDto> TargetFiles { get; init; } = [];
    public List<FileReferenceDto> Attachments { get; init; } = [];
    public List<TaskDependencyResponse> BlockedBy { get; init; } = [];
    public List<TaskDependencyResponse> Blocking { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public List<StateChangeDto>? StateChanges { get; set; }
}
