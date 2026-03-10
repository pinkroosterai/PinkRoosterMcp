using System.ComponentModel;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Mcp.Inputs;

public sealed class ScaffoldPhaseInput
{
    [Description("Phase name.")]
    public required string Name { get; set; }

    [Description("Phase description.")]
    public string? Description { get; set; }

    [Description("Sort order for display ordering.")]
    public int? SortOrder { get; set; }

    [Description("Acceptance criteria for this phase.")]
    public List<AcceptanceCriterionInput>? AcceptanceCriteria { get; set; }

    [Description("Tasks in this phase. Each requires name + description.")]
    public List<ScaffoldTaskInput>? Tasks { get; set; }
}

public sealed class ScaffoldTaskInput
{
    [Description("Task name.")]
    public required string Name { get; set; }

    [Description("Task description.")]
    public required string Description { get; set; }

    [Description("Sort order for display ordering.")]
    public int? SortOrder { get; set; }

    [Description("Implementation notes.")]
    public string? ImplementationNotes { get; set; }

    [Description("Completion state. Default: NotStarted.")]
    public CompletionState? State { get; set; }

    [Description("Target files for this task.")]
    public List<FileReferenceInput>? TargetFiles { get; set; }

    [Description("File attachments.")]
    public List<FileReferenceInput>? Attachments { get; set; }

    [Description("0-based indices of other tasks within this phase that must complete first.")]
    public List<int>? DependsOnTaskIndices { get; set; }
}
