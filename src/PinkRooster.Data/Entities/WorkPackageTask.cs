using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Entities;

public sealed class WorkPackageTask : IHasBlockedState, IHasUpdatedAt
{
    public long Id { get; set; }
    public int TaskNumber { get; set; }
    public long PhaseId { get; set; }
    public WorkPackagePhase Phase { get; set; } = null!;
    public long WorkPackageId { get; set; }
    public WorkPackage WorkPackage { get; set; } = null!;

    // ── Definition ──
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int SortOrder { get; set; }
    public string? ImplementationNotes { get; set; }

    // ── State ──
    public CompletionState State { get; set; } = CompletionState.NotStarted;
    public CompletionState? PreviousActiveState { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // ── Files (jsonb) ──
    public List<FileReference> TargetFiles { get; set; } = [];
    public List<FileReference> Attachments { get; set; } = [];

    // ── Dependencies ──
    public List<WorkPackageTaskDependency> BlockedBy { get; set; } = [];
    public List<WorkPackageTaskDependency> Blocking { get; set; } = [];

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
