namespace PinkRooster.Data.Entities;

public sealed class UserSession : IHasUpdatedAt
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Token { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
