using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Entities;

public sealed class WorkPackagePhase
{
    public long Id { get; set; }
    public int PhaseNumber { get; set; }
    public long WorkPackageId { get; set; }
    public WorkPackage WorkPackage { get; set; } = null!;

    // ── Definition ──
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    // ── State ──
    public CompletionState State { get; set; } = CompletionState.NotStarted;

    // ── Children ──
    public List<WorkPackageTask> Tasks { get; set; } = [];
    public List<AcceptanceCriterion> AcceptanceCriteria { get; set; } = [];

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
