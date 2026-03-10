namespace PinkRooster.Shared.DTOs.Responses;

public sealed class BatchUpdateTaskStatesResponse
{
    public required int UpdatedCount { get; init; }
    public List<TaskStateResult> Results { get; init; } = [];
    public List<StateChangeDto>? StateChanges { get; set; }
}

public sealed class TaskStateResult
{
    public required string TaskId { get; init; }
    public required string OldState { get; init; }
    public required string NewState { get; init; }
}
