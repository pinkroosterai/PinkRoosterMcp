namespace PinkRooster.Data.Entities;

/// <summary>
/// Marker interface for entities with state-driven timestamps.
/// Used by StateTransitionHelper to apply timestamp rules consistently.
/// </summary>
public interface IHasStateTimestamps
{
    DateTimeOffset? StartedAt { get; set; }
    DateTimeOffset? CompletedAt { get; set; }
    DateTimeOffset? ResolvedAt { get; set; }
}
