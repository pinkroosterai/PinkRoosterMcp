using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

/// <summary>
/// Shared static methods for state transition logic.
/// Eliminates duplication of timestamp, blocked-state, and file-mapping logic across services.
/// </summary>
public static class StateTransitionHelper
{
    /// <summary>
    /// Applies state-driven timestamps based on CompletionState transitions.
    /// Rules:
    /// - StartedAt: set once when entering an active state (never cleared)
    /// - CompletedAt: set when → Completed (cleared when leaving terminal)
    /// - ResolvedAt: set when → any terminal state (cleared when leaving terminal)
    /// </summary>
    public static void ApplyStateTimestamps(IHasStateTimestamps entity, CompletionState oldState, CompletionState newState)
    {
        if (oldState == newState)
            return;

        var now = DateTimeOffset.UtcNow;

        // StartedAt: set once when entering an active state from inactive
        if (entity.StartedAt is null && CompletionStateConstants.ActiveStates.Contains(newState))
            entity.StartedAt = now;

        // CompletedAt: set when entering Completed, cleared when leaving terminal
        if (newState == CompletionState.Completed)
            entity.CompletedAt = now;
        else if (CompletionStateConstants.TerminalStates.Contains(oldState) && !CompletionStateConstants.TerminalStates.Contains(newState))
            entity.CompletedAt = null;

        // ResolvedAt: set when entering any terminal state, cleared when leaving terminal
        if (CompletionStateConstants.TerminalStates.Contains(newState))
            entity.ResolvedAt = now;
        else if (CompletionStateConstants.TerminalStates.Contains(oldState))
            entity.ResolvedAt = null;
    }

    /// <summary>
    /// Manages PreviousActiveState for entities that support auto-block/unblock.
    /// - Transitioning TO Blocked from an active state: captures PreviousActiveState
    /// - Transitioning FROM Blocked: clears PreviousActiveState
    /// </summary>
    public static void ApplyBlockedStateLogic(IHasBlockedState entity, CompletionState oldState, CompletionState newState)
    {
        // Transitioning TO Blocked: capture previous active state
        if (newState == CompletionState.Blocked && CompletionStateConstants.ActiveStates.Contains(oldState))
            entity.PreviousActiveState = oldState;

        // Transitioning FROM Blocked: clear previous active state
        if (oldState == CompletionState.Blocked && newState != CompletionState.Blocked)
            entity.PreviousActiveState = null;
    }

    /// <summary>
    /// Maps FileReferenceDto list to FileReference entity list.
    /// Shared across all services that handle attachments or target files.
    /// </summary>
    public static List<FileReference> MapFileReferences(List<FileReferenceDto>? dtos)
    {
        if (dtos is null or { Count: 0 })
            return [];

        return dtos.Select(d => new FileReference
        {
            FileName = d.FileName,
            RelativePath = d.RelativePath,
            Description = d.Description
        }).ToList();
    }
}
