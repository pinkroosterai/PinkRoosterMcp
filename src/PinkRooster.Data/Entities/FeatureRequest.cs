using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Entities;

public sealed class FeatureRequest : IHasStateTimestamps, IHasUpdatedAt
{
    public long Id { get; set; }
    public int FeatureRequestNumber { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    // ── Definition ──
    public required string Name { get; set; }
    public required string Description { get; set; }
    public FeatureCategory Category { get; set; } = FeatureCategory.Feature;
    public Priority Priority { get; set; } = Priority.Medium;

    // ── Context ──
    public string? BusinessValue { get; set; }
    public List<UserStory> UserStories { get; set; } = [];
    public string? Requester { get; set; }
    public string? AcceptanceSummary { get; set; }

    // ── State ──
    public FeatureStatus Status { get; set; } = FeatureStatus.Proposed;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // ── Attachments (jsonb) ──
    public List<FileReference> Attachments { get; set; } = [];

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
