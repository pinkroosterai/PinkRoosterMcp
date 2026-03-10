namespace PinkRooster.Shared.DTOs.Responses;

public sealed class TaskDependencyResponse
{
    public required string TaskId { get; init; }
    public required string Name { get; init; }
    public required string State { get; init; }
    public string? Reason { get; init; }
    public List<StateChangeDto>? StateChanges { get; set; }
}
