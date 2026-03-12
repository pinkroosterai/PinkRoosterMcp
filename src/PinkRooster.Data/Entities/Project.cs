using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Entities;

public sealed class Project : IHasUpdatedAt
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string ProjectPath { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    // ── Sequential number counters (monotonically increasing, never decremented on deletion) ──
    public int NextIssueNumber { get; set; } = 1;
    public int NextFrNumber { get; set; } = 1;
    public int NextWpNumber { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
