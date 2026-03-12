namespace PinkRooster.Data.Entities;

public sealed class ProjectMemory : IHasUpdatedAt
{
    public long Id { get; set; }
    public int MemoryNumber { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    // ── Definition ──
    public required string Name { get; set; }
    public required string Content { get; set; }
    public List<string> Tags { get; set; } = [];

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
