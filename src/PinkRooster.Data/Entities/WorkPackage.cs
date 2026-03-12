using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Entities;

public sealed class WorkPackage : IHasBlockedState, IHasUpdatedAt
{
    public long Id { get; set; }
    public int WorkPackageNumber { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    // ── Entity Links (many-to-many via join tables) ──
    public List<WorkPackageIssueLink> LinkedIssueLinks { get; set; } = [];
    public List<WorkPackageFeatureRequestLink> LinkedFeatureRequestLinks { get; set; } = [];

    // ── Definition ──
    public required string Name { get; set; }
    public required string Description { get; set; }
    public WorkPackageType Type { get; set; } = WorkPackageType.Feature;
    public Priority Priority { get; set; } = Priority.Medium;
    public string? Plan { get; set; }

    // ── Estimation ──
    public int? EstimatedComplexity { get; set; }
    public string? EstimationRationale { get; set; }

    // ── State ──
    public CompletionState State { get; set; } = CompletionState.NotStarted;
    public CompletionState? PreviousActiveState { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // ── Attachments (jsonb) ──
    public List<FileReference> Attachments { get; set; } = [];

    // ── Sequential number counters (monotonically increasing, never decremented on deletion) ──
    public int NextPhaseNumber { get; set; } = 1;
    public int NextTaskNumber { get; set; } = 1;

    // ── Children ──
    public List<WorkPackagePhase> Phases { get; set; } = [];

    // ── Dependencies ──
    public List<WorkPackageDependency> BlockedBy { get; set; } = [];
    public List<WorkPackageDependency> Blocking { get; set; } = [];

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
