namespace PinkRooster.Shared.DTOs.Responses;

public sealed class ScaffoldWorkPackageResponse
{
    public required string WorkPackageId { get; init; }
    public required List<ScaffoldPhaseResult> Phases { get; init; }
    public int TotalTasks { get; init; }
    public int TotalDependencies { get; init; }
    public List<StateChangeDto>? StateChanges { get; set; }
}

public sealed class ScaffoldPhaseResult
{
    public required string PhaseId { get; init; }
    public required List<string> TaskIds { get; init; }
}
