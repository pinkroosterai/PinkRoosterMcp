using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class StateCascadeService(AppDbContext db) : IStateCascadeService
{
    public async Task PropagateStateUpwardAsync(
        long phaseId, WorkPackage wp, string changedBy, List<StateChangeDto>? stateChanges, CancellationToken ct, List<WorkPackagePhase>? preloadedPhases = null)
    {
        var now = DateTimeOffset.UtcNow;

        // ── Upward activation: auto-activate phase and WP when tasks become active ──
        var phaseTasks = await db.WorkPackageTasks
            .Where(t => t.PhaseId == phaseId)
            .ToListAsync(ct);

        var firstActiveTask = phaseTasks.FirstOrDefault(t => CompletionStateConstants.ActiveStates.Contains(t.State));
        if (firstActiveTask is not null)
        {
            var phase = await db.WorkPackagePhases.FirstAsync(p => p.Id == phaseId, ct);
            if (phase.State == CompletionState.NotStarted)
            {
                var oldPhaseState = phase.State;
                phase.State = firstActiveTask.State;

                db.PhaseAuditLogs.Add(new PhaseAuditLog
                {
                    PhaseId = phase.Id,
                    FieldName = "State",
                    OldValue = oldPhaseState.ToString(),
                    NewValue = firstActiveTask.State.ToString(),
                    ChangedBy = changedBy,
                    ChangedAt = now
                });

                stateChanges?.Add(new StateChangeDto
                {
                    EntityType = "Phase",
                    EntityId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-phase-{phase.PhaseNumber}",
                    OldState = oldPhaseState.ToString(),
                    NewState = firstActiveTask.State.ToString(),
                    Reason = "Auto-activated: task transitioned to active state"
                });
            }

            if (wp.State == CompletionState.NotStarted)
            {
                var oldWpState = wp.State;
                wp.State = firstActiveTask.State;
                StateTransitionHelper.ApplyStateTimestamps(wp, oldWpState, firstActiveTask.State);

                db.WorkPackageAuditLogs.Add(new WorkPackageAuditLog
                {
                    WorkPackageId = wp.Id,
                    FieldName = "State",
                    OldValue = oldWpState.ToString(),
                    NewValue = firstActiveTask.State.ToString(),
                    ChangedBy = changedBy,
                    ChangedAt = now
                });

                stateChanges?.Add(new StateChangeDto
                {
                    EntityType = "WorkPackage",
                    EntityId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}",
                    OldState = oldWpState.ToString(),
                    NewState = firstActiveTask.State.ToString(),
                    Reason = "Auto-activated: task transitioned to active state"
                });
            }
        }

        // ── Downward completion: auto-complete phase and WP when all tasks are terminal ──

        if (phaseTasks.Count > 0 && phaseTasks.All(t => CompletionStateConstants.TerminalStates.Contains(t.State)))
        {
            var phase = await db.WorkPackagePhases.FirstAsync(p => p.Id == phaseId, ct);
            if (!CompletionStateConstants.TerminalStates.Contains(phase.State))
            {
                var oldPhaseState = phase.State;
                phase.State = CompletionState.Completed;

                db.PhaseAuditLogs.Add(new PhaseAuditLog
                {
                    PhaseId = phase.Id,
                    FieldName = "State",
                    OldValue = oldPhaseState.ToString(),
                    NewValue = CompletionState.Completed.ToString(),
                    ChangedBy = changedBy,
                    ChangedAt = now
                });

                stateChanges?.Add(new StateChangeDto
                {
                    EntityType = "Phase",
                    EntityId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-phase-{phase.PhaseNumber}",
                    OldState = oldPhaseState.ToString(),
                    NewState = CompletionState.Completed.ToString(),
                    Reason = "Auto-completed: all tasks reached terminal state"
                });
            }
        }

        // Auto-complete empty phases (no tasks = nothing left to do)
        // Use preloaded phases if available (batch callers), otherwise query
        var allPhases = preloadedPhases ?? await db.WorkPackagePhases
            .Where(p => p.WorkPackageId == wp.Id)
            .ToListAsync(ct);

        var nonTerminalPhaseIds = allPhases
            .Where(p => !CompletionStateConstants.TerminalStates.Contains(p.State))
            .Select(p => p.Id)
            .ToList();

        var phaseIdsWithTasks = nonTerminalPhaseIds.Count > 0
            ? (await db.WorkPackageTasks
                .Where(t => nonTerminalPhaseIds.Contains(t.PhaseId))
                .Select(t => t.PhaseId)
                .Distinct()
                .ToListAsync(ct))
                .ToHashSet()
            : [];

        foreach (var emptyPhase in allPhases.Where(p => nonTerminalPhaseIds.Contains(p.Id) && !phaseIdsWithTasks.Contains(p.Id)))
        {
            var oldEmptyPhaseState = emptyPhase.State;
            emptyPhase.State = CompletionState.Completed;

            db.PhaseAuditLogs.Add(new PhaseAuditLog
            {
                PhaseId = emptyPhase.Id,
                FieldName = "State",
                OldValue = oldEmptyPhaseState.ToString(),
                NewValue = CompletionState.Completed.ToString(),
                ChangedBy = changedBy,
                ChangedAt = now
            });

            stateChanges?.Add(new StateChangeDto
            {
                EntityType = "Phase",
                EntityId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-phase-{emptyPhase.PhaseNumber}",
                OldState = oldEmptyPhaseState.ToString(),
                NewState = CompletionState.Completed.ToString(),
                Reason = "Auto-completed: phase has no tasks"
            });
        }

        // Check if all phases in WP are terminal
        if (allPhases.Count > 0 && allPhases.All(p => CompletionStateConstants.TerminalStates.Contains(p.State)))
        {
            if (!CompletionStateConstants.TerminalStates.Contains(wp.State))
            {
                var oldWpState = wp.State;
                wp.State = CompletionState.Completed;

                // Use shared timestamp logic (fixes the inline-assignment divergence)
                StateTransitionHelper.ApplyStateTimestamps(wp, oldWpState, CompletionState.Completed);

                db.WorkPackageAuditLogs.Add(new WorkPackageAuditLog
                {
                    WorkPackageId = wp.Id,
                    FieldName = "State",
                    OldValue = oldWpState.ToString(),
                    NewValue = CompletionState.Completed.ToString(),
                    ChangedBy = changedBy,
                    ChangedAt = now
                });

                stateChanges?.Add(new StateChangeDto
                {
                    EntityType = "WorkPackage",
                    EntityId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}",
                    OldState = oldWpState.ToString(),
                    NewState = CompletionState.Completed.ToString(),
                    Reason = "Auto-completed: all phases reached terminal state"
                });
            }
        }
    }

    public async Task AutoUnblockDependentWpsAsync(
        WorkPackage completedWp, List<StateChangeDto>? stateChanges, CancellationToken ct)
    {
        // Find WPs that depend on this WP and are currently Blocked
        var dependentWps = await db.WorkPackageDependencies
            .Where(d => d.DependsOnWorkPackageId == completedWp.Id)
            .Include(d => d.DependentWorkPackage)
            .Where(d => d.DependentWorkPackage.State == CompletionState.Blocked)
            .Select(d => d.DependentWorkPackage)
            .ToListAsync(ct);

        foreach (var dependentWp in dependentWps)
        {
            // Check if any remaining non-terminal blockers exist
            var hasRemainingBlockers = await db.WorkPackageDependencies
                .Where(d => d.DependentWorkPackageId == dependentWp.Id && d.DependsOnWorkPackageId != completedWp.Id)
                .Include(d => d.DependsOnWorkPackage)
                .AnyAsync(d => !CompletionStateConstants.TerminalStates.Contains(d.DependsOnWorkPackage.State), ct);

            if (!hasRemainingBlockers && dependentWp.PreviousActiveState is not null)
            {
                var oldState = dependentWp.State;
                var restoredState = dependentWp.PreviousActiveState.Value;

                dependentWp.State = restoredState;
                dependentWp.PreviousActiveState = null;
                StateTransitionHelper.ApplyStateTimestamps(dependentWp, oldState, restoredState);

                var now = DateTimeOffset.UtcNow;
                db.WorkPackageAuditLogs.Add(new WorkPackageAuditLog
                {
                    WorkPackageId = dependentWp.Id,
                    FieldName = "State",
                    OldValue = oldState.ToString(),
                    NewValue = restoredState.ToString(),
                    ChangedBy = "system",
                    ChangedAt = now
                });

                stateChanges?.Add(new StateChangeDto
                {
                    EntityType = "WorkPackage",
                    EntityId = $"proj-{dependentWp.ProjectId}-wp-{dependentWp.WorkPackageNumber}",
                    OldState = oldState.ToString(),
                    NewState = restoredState.ToString(),
                    Reason = $"Auto-unblocked: blocker 'proj-{completedWp.ProjectId}-wp-{completedWp.WorkPackageNumber}' completed"
                });
            }
        }
    }

    public async Task AutoUnblockDependentTasksAsync(
        WorkPackageTask completedTask, WorkPackage wp, List<StateChangeDto>? stateChanges, CancellationToken ct)
    {
        var dependentTasks = await db.WorkPackageTaskDependencies
            .Where(d => d.DependsOnTaskId == completedTask.Id)
            .Include(d => d.DependentTask)
            .Where(d => d.DependentTask.State == CompletionState.Blocked)
            .Select(d => d.DependentTask)
            .ToListAsync(ct);

        foreach (var dependentTask in dependentTasks)
        {
            var hasRemainingBlockers = await db.WorkPackageTaskDependencies
                .Where(d => d.DependentTaskId == dependentTask.Id && d.DependsOnTaskId != completedTask.Id)
                .Include(d => d.DependsOnTask)
                .AnyAsync(d => !CompletionStateConstants.TerminalStates.Contains(d.DependsOnTask.State), ct);

            if (!hasRemainingBlockers && dependentTask.PreviousActiveState is not null)
            {
                var oldState = dependentTask.State;
                var restoredState = dependentTask.PreviousActiveState.Value;

                dependentTask.State = restoredState;
                dependentTask.PreviousActiveState = null;
                StateTransitionHelper.ApplyStateTimestamps(dependentTask, oldState, restoredState);

                var now = DateTimeOffset.UtcNow;
                db.TaskAuditLogs.Add(new TaskAuditLog
                {
                    TaskId = dependentTask.Id,
                    FieldName = "State",
                    OldValue = oldState.ToString(),
                    NewValue = restoredState.ToString(),
                    ChangedBy = "system",
                    ChangedAt = now
                });

                stateChanges?.Add(new StateChangeDto
                {
                    EntityType = "Task",
                    EntityId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{dependentTask.TaskNumber}",
                    OldState = oldState.ToString(),
                    NewState = restoredState.ToString(),
                    Reason = $"Auto-unblocked: blocker 'proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{completedTask.TaskNumber}' completed"
                });
            }
        }
    }

    public void AutoBlockWpIfNeeded(WorkPackage dependentWp, WorkPackage blockerWp, List<StateChangeDto>? stateChanges)
    {
        if (CompletionStateConstants.TerminalStates.Contains(blockerWp.State)
            || dependentWp.State == CompletionState.Blocked
            || CompletionStateConstants.TerminalStates.Contains(dependentWp.State)
            || CompletionStateConstants.InactiveStates.Contains(dependentWp.State))
            return;

        var oldState = dependentWp.State;
        dependentWp.PreviousActiveState = oldState;
        dependentWp.State = CompletionState.Blocked;
        StateTransitionHelper.ApplyStateTimestamps(dependentWp, oldState, CompletionState.Blocked);

        db.WorkPackageAuditLogs.Add(new WorkPackageAuditLog
        {
            WorkPackage = dependentWp,
            FieldName = "State",
            OldValue = oldState.ToString(),
            NewValue = CompletionState.Blocked.ToString(),
            ChangedBy = "system",
            ChangedAt = DateTimeOffset.UtcNow
        });

        stateChanges?.Add(new StateChangeDto
        {
            EntityType = "WorkPackage",
            EntityId = $"proj-{dependentWp.ProjectId}-wp-{dependentWp.WorkPackageNumber}",
            OldState = oldState.ToString(),
            NewState = CompletionState.Blocked.ToString(),
            Reason = $"Auto-blocked: dependency on 'proj-{blockerWp.ProjectId}-wp-{blockerWp.WorkPackageNumber}' added"
        });
    }

    public void AutoBlockTaskIfNeeded(WorkPackageTask dependentTask, WorkPackageTask blockerTask, WorkPackage wp, List<StateChangeDto>? stateChanges)
    {
        if (CompletionStateConstants.TerminalStates.Contains(blockerTask.State)
            || dependentTask.State == CompletionState.Blocked
            || CompletionStateConstants.TerminalStates.Contains(dependentTask.State)
            || CompletionStateConstants.InactiveStates.Contains(dependentTask.State))
            return;

        var oldState = dependentTask.State;
        dependentTask.PreviousActiveState = oldState;
        dependentTask.State = CompletionState.Blocked;
        StateTransitionHelper.ApplyStateTimestamps(dependentTask, oldState, CompletionState.Blocked);

        db.TaskAuditLogs.Add(new TaskAuditLog
        {
            Task = dependentTask,
            FieldName = "State",
            OldValue = oldState.ToString(),
            NewValue = CompletionState.Blocked.ToString(),
            ChangedBy = "system",
            ChangedAt = DateTimeOffset.UtcNow
        });

        stateChanges?.Add(new StateChangeDto
        {
            EntityType = "Task",
            EntityId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{dependentTask.TaskNumber}",
            OldState = oldState.ToString(),
            NewState = CompletionState.Blocked.ToString(),
            Reason = $"Auto-blocked: dependency on 'proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{blockerTask.TaskNumber}' added"
        });
    }

    public async Task<bool> HasCircularWpDependencyAsync(long dependentId, long dependsOnId, CancellationToken ct)
    {
        return await HasCircularDependencyAsync(
            dependentId, dependsOnId,
            currentId => db.WorkPackageDependencies
                .Where(d => d.DependentWorkPackageId == currentId)
                .Select(d => d.DependsOnWorkPackageId)
                .ToListAsync(ct),
            ct);
    }

    public async Task<bool> HasCircularTaskDependencyAsync(long dependentId, long dependsOnId, CancellationToken ct)
    {
        return await HasCircularDependencyAsync(
            dependentId, dependsOnId,
            currentId => db.WorkPackageTaskDependencies
                .Where(d => d.DependentTaskId == currentId)
                .Select(d => d.DependsOnTaskId)
                .ToListAsync(ct),
            ct);
    }

    /// <summary>
    /// Generic BFS cycle detection for any dependency graph.
    /// </summary>
    private static async Task<bool> HasCircularDependencyAsync(
        long dependentId, long dependsOnId, Func<long, Task<List<long>>> getNeighbors, CancellationToken ct)
    {
        var visited = new HashSet<long>();
        var queue = new Queue<long>();
        queue.Enqueue(dependsOnId);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var currentId = queue.Dequeue();
            if (currentId == dependentId)
                return true;

            if (!visited.Add(currentId))
                continue;

            var neighborIds = await getNeighbors(currentId);
            foreach (var id in neighborIds)
                queue.Enqueue(id);
        }

        return false;
    }
}
