using PinkRooster.Shared.Enums;

namespace PinkRooster.Data.Entities;

/// <summary>
/// Interface for entities that support auto-block/unblock via dependencies.
/// Extends IHasStateTimestamps since blocked entities also need timestamp tracking.
/// </summary>
public interface IHasBlockedState : IHasStateTimestamps
{
    CompletionState? PreviousActiveState { get; set; }
}
