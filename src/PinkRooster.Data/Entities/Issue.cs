using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Entities;

public sealed class Issue
{
    public long Id { get; set; }
    public int IssueNumber { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    // ── Definition ──
    public required string Name { get; set; }
    public required string Description { get; set; }
    public IssueType IssueType { get; set; }
    public IssueSeverity Severity { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;

    // ── Reproduction / diagnosis ──
    public string? StepsToReproduce { get; set; }
    public string? ExpectedBehavior { get; set; }
    public string? ActualBehavior { get; set; }
    public string? AffectedComponent { get; set; }
    public string? StackTrace { get; set; }

    // ── Resolution ──
    public string? RootCause { get; set; }
    public string? Resolution { get; set; }

    // ── State ──
    public CompletionState State { get; set; } = CompletionState.NotStarted;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // ── Attachments ──
    public List<FileReference> Attachments { get; set; } = [];

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
