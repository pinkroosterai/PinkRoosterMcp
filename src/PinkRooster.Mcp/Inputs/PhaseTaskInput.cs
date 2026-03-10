using System.ComponentModel;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Mcp.Inputs;

public sealed class PhaseTaskInput
{
    [Description("Task number (for updating an existing task). Omit when creating new tasks.")]
    public int? TaskNumber { get; set; }

    [Description("Task name. Required when creating a new task.")]
    public string? Name { get; set; }

    [Description("Task description. Required when creating a new task.")]
    public string? Description { get; set; }

    [Description("Sort order for display ordering.")]
    public int? SortOrder { get; set; }

    [Description("Implementation notes.")]
    public string? ImplementationNotes { get; set; }

    [Description("Completion state.")]
    public CompletionState? State { get; set; }

    [Description("Target files for this task.")]
    public List<FileReferenceInput>? TargetFiles { get; set; }

    [Description("File attachments.")]
    public List<FileReferenceInput>? Attachments { get; set; }
}
