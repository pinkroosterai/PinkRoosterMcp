using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Entities;

public sealed class User : IHasUpdatedAt
{
    public long Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public GlobalRole GlobalRole { get; set; } = GlobalRole.User;
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }

    // ── Navigation ──
    public ICollection<UserSession> Sessions { get; set; } = [];
    public ICollection<UserProjectRole> ProjectRoles { get; set; } = [];

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
