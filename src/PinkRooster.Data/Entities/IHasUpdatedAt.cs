namespace PinkRooster.Data.Entities;

/// <summary>
/// Marker interface for entities whose UpdatedAt is auto-set by AppDbContext.SaveChangesAsync.
/// </summary>
public interface IHasUpdatedAt
{
    DateTimeOffset UpdatedAt { get; set; }
}
