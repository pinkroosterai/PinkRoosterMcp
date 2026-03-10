using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class BatchUpdateTaskStatesRequest
{
    public required List<TaskStateUpdate> Tasks { get; init; }
}

public sealed class TaskStateUpdate
{
    public required int TaskNumber { get; init; }
    public required CompletionState State { get; init; }
}
