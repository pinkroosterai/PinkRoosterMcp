using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Entities;

public sealed class UserProjectRole : IHasUpdatedAt
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public ProjectRole Role { get; set; }

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
