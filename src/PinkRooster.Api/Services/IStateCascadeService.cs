using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

/// <summary>
/// Handles cross-entity state cascades: upward propagation (task→phase→WP auto-complete),
/// auto-block/unblock of dependents, and circular dependency detection.
/// </summary>
public interface IStateCascadeService
{
    /// <summary>
    /// Checks if all tasks in a phase are terminal (→ auto-complete phase),
    /// then checks if all phases in WP are terminal (→ auto-complete WP).
    /// </summary>
    Task PropagateStateUpwardAsync(long phaseId, WorkPackage wp, string changedBy, List<StateChangeDto>? stateChanges, CancellationToken ct);

    /// <summary>
    /// When a WP reaches terminal state, finds blocked dependent WPs and restores their PreviousActiveState.
    /// </summary>
    Task AutoUnblockDependentWpsAsync(WorkPackage completedWp, List<StateChangeDto>? stateChanges, CancellationToken ct);

    /// <summary>
    /// When a task reaches terminal state, finds blocked dependent tasks and restores their PreviousActiveState.
    /// </summary>
    Task AutoUnblockDependentTasksAsync(WorkPackageTask completedTask, WorkPackage wp, List<StateChangeDto>? stateChanges, CancellationToken ct);

    /// <summary>
    /// If the blocker WP is non-terminal and the dependent WP is active, transitions dependent to Blocked.
    /// </summary>
    void AutoBlockWpIfNeeded(WorkPackage dependentWp, WorkPackage blockerWp, List<StateChangeDto>? stateChanges);

    /// <summary>
    /// If the blocker task is non-terminal and the dependent task is active, transitions dependent to Blocked.
    /// </summary>
    void AutoBlockTaskIfNeeded(WorkPackageTask dependentTask, WorkPackageTask blockerTask, WorkPackage wp, List<StateChangeDto>? stateChanges);

    /// <summary>
    /// BFS cycle detection for work package dependencies.
    /// </summary>
    Task<bool> HasCircularWpDependencyAsync(long dependentId, long dependsOnId, CancellationToken ct);

    /// <summary>
    /// BFS cycle detection for task dependencies.
    /// </summary>
    Task<bool> HasCircularTaskDependencyAsync(long dependentId, long dependsOnId, CancellationToken ct);
}
