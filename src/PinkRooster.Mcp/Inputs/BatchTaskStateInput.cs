using System.ComponentModel;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Mcp.Inputs;

public sealed class BatchTaskStateInput
{
    [Description("Task ID (e.g. 'proj-1-wp-2-task-3').")]
    public required string TaskId { get; set; }

    [Description("Target completion state (e.g. Completed, Implementing).")]
    public required CompletionState State { get; set; }
}
