using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class WorkPackageTaskService(AppDbContext db, IStateCascadeService cascadeService, IEventBroadcaster broadcaster) : IWorkPackageTaskService
{
    public async Task<TaskResponse> CreateAsync(
        long projectId, int wpNumber, int phaseNumber, CreateTaskRequest request, string changedBy, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async (cancellation) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellation);

            var wp = await db.WorkPackages
                .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, cancellation)
                ?? throw new KeyNotFoundException($"Work package {wpNumber} not found in project {projectId}");

            var phase = await db.WorkPackagePhases
                .FirstOrDefaultAsync(p => p.WorkPackageId == wp.Id && p.PhaseNumber == phaseNumber, cancellation)
                ?? throw new InvalidOperationException($"Phase {phaseNumber} not found in work package {wpNumber}");

            // TaskNumber is sequential across ALL phases within the WP
            var nextTaskNumber = await db.WorkPackageTasks
                .Where(t => t.WorkPackageId == wp.Id)
                .MaxAsync(t => (int?)t.TaskNumber, cancellation) ?? 0;
            nextTaskNumber++;

            // SortOrder: auto-assign within the target phase if not provided
            var sortOrder = request.SortOrder;
            if (sortOrder is null)
            {
                sortOrder = await db.WorkPackageTasks
                    .Where(t => t.PhaseId == phase.Id)
                    .MaxAsync(t => (int?)t.SortOrder, cancellation) ?? 0;
                sortOrder++;
            }

            var task = new WorkPackageTask
            {
                TaskNumber = nextTaskNumber,
                PhaseId = phase.Id,
                WorkPackageId = wp.Id,
                Name = request.Name,
                Description = request.Description,
                SortOrder = sortOrder.Value,
                ImplementationNotes = request.ImplementationNotes,
                State = request.State,
                TargetFiles = StateTransitionHelper.MapFileReferences(request.TargetFiles),
                Attachments = StateTransitionHelper.MapFileReferences(request.Attachments)
            };

            // Apply state-driven timestamps
            StateTransitionHelper.ApplyStateTimestamps(task, CompletionState.NotStarted, request.State);

            // Apply blocked state logic
            StateTransitionHelper.ApplyBlockedStateLogic(task, CompletionState.NotStarted, request.State);

            db.WorkPackageTasks.Add(task);

            // Audit all fields on creation
            var auditEntries = BuildCreateAuditEntries(task, changedBy);
            db.TaskAuditLogs.AddRange(auditEntries);

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);

            broadcaster.Publish(new ServerEvent
            {
                EventType = "entity:changed",
                EntityType = "Task",
                EntityId = $"proj-{projectId}-wp-{wpNumber}-task-{task.TaskNumber}",
                Action = "created",
                ProjectId = projectId
            });

            return ToResponse(task, wp, phase);
        }, ct);
    }

    public async Task<TaskResponse?> UpdateAsync(
        long projectId, int wpNumber, int taskNumber, UpdateTaskRequest request, string changedBy, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default)
    {
        stateChanges ??= [];

        var wp = await db.WorkPackages
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct);

        if (wp is null)
            return null;

        var task = await db.WorkPackageTasks
            .Include(t => t.Phase)
            .Include(t => t.BlockedBy).ThenInclude(d => d.DependsOnTask)
            .Include(t => t.Blocking).ThenInclude(d => d.DependentTask)
            .FirstOrDefaultAsync(t => t.WorkPackageId == wp.Id && t.TaskNumber == taskNumber, ct);

        if (task is null)
            return null;

        var auditEntries = new List<TaskAuditLog>();
        var now = DateTimeOffset.UtcNow;

        // Track state before changes for timestamp logic
        var oldState = task.State;
        var oldPhaseId = task.PhaseId;

        if (request.Name is not null)
            AuditAndSet(auditEntries, task.Id, changedBy, now, "Name", task.Name, request.Name, v => task.Name = v);

        if (request.Description is not null)
            AuditAndSet(auditEntries, task.Id, changedBy, now, "Description", task.Description, request.Description, v => task.Description = v);

        if (request.SortOrder is not null)
            AuditAndSetInt(auditEntries, task.Id, changedBy, now, "SortOrder", task.SortOrder, request.SortOrder.Value, v => task.SortOrder = v);

        if (request.ImplementationNotes is not null)
            AuditAndSet(auditEntries, task.Id, changedBy, now, "ImplementationNotes", task.ImplementationNotes, request.ImplementationNotes, v => task.ImplementationNotes = v);

        if (request.State is not null)
            AuditAndSetEnum(auditEntries, task.Id, changedBy, now, "State", task.State, request.State.Value, v => task.State = v);

        if (request.TargetFiles is not null)
        {
            var oldJson = JsonSerializer.Serialize(task.TargetFiles.Select(f => new { f.FileName, f.RelativePath, f.Description }));
            task.TargetFiles = StateTransitionHelper.MapFileReferences(request.TargetFiles);
            var newJson = JsonSerializer.Serialize(task.TargetFiles.Select(f => new { f.FileName, f.RelativePath, f.Description }));

            if (oldJson != newJson)
            {
                auditEntries.Add(new TaskAuditLog
                {
                    TaskId = task.Id,
                    FieldName = "TargetFiles",
                    OldValue = oldJson,
                    NewValue = newJson,
                    ChangedBy = changedBy,
                    ChangedAt = now
                });
            }
        }

        if (request.Attachments is not null)
        {
            var oldJson = JsonSerializer.Serialize(task.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description }));
            task.Attachments = StateTransitionHelper.MapFileReferences(request.Attachments);
            var newJson = JsonSerializer.Serialize(task.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description }));

            if (oldJson != newJson)
            {
                auditEntries.Add(new TaskAuditLog
                {
                    TaskId = task.Id,
                    FieldName = "Attachments",
                    OldValue = oldJson,
                    NewValue = newJson,
                    ChangedBy = changedBy,
                    ChangedAt = now
                });
            }
        }

        // PhaseId move: validate target phase exists in same WP
        if (request.PhaseId is not null && request.PhaseId.Value != task.PhaseId)
        {
            var targetPhase = await db.WorkPackagePhases
                .FirstOrDefaultAsync(p => p.Id == request.PhaseId.Value && p.WorkPackageId == wp.Id, ct)
                ?? throw new InvalidOperationException($"Target phase {request.PhaseId.Value} not found in work package {wpNumber}");

            auditEntries.Add(new TaskAuditLog
            {
                TaskId = task.Id,
                FieldName = "PhaseId",
                OldValue = task.PhaseId.ToString(),
                NewValue = targetPhase.Id.ToString(),
                ChangedBy = changedBy,
                ChangedAt = now
            });

            task.PhaseId = targetPhase.Id;
            task.Phase = targetPhase;
        }

        // Apply blocked state logic and state-driven timestamps if state changed
        if (request.State is not null && oldState != request.State.Value)
        {
            var newState = request.State.Value;
            StateTransitionHelper.ApplyBlockedStateLogic(task, oldState, newState);
            StateTransitionHelper.ApplyStateTimestamps(task, oldState, newState);
        }

        if (auditEntries.Count > 0)
            db.TaskAuditLogs.AddRange(auditEntries);

        // Upward propagation check after state change
        if (request.State is not null && oldState != request.State.Value)
        {
            await cascadeService.PropagateStateUpwardAsync(task.PhaseId, wp, changedBy, stateChanges, ct);

            // If task was moved between phases, also check old phase
            if (oldPhaseId != task.PhaseId)
                await cascadeService.PropagateStateUpwardAsync(oldPhaseId, wp, changedBy, stateChanges, ct);

            // Dep-completion auto-unblock: if task transitioned to terminal, unblock dependent tasks
            if (CompletionStateConstants.TerminalStates.Contains(request.State.Value))
                await cascadeService.AutoUnblockDependentTasksAsync(task, wp, stateChanges, ct);
        }

        // If phase changed (without state change), check both old and new phase
        if (request.PhaseId is not null && request.PhaseId.Value != oldPhaseId && (request.State is null || oldState == request.State.Value))
        {
            await cascadeService.PropagateStateUpwardAsync(task.PhaseId, wp, changedBy, stateChanges, ct);
            await cascadeService.PropagateStateUpwardAsync(oldPhaseId, wp, changedBy, stateChanges, ct);
        }

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "Task",
            EntityId = $"proj-{projectId}-wp-{wpNumber}-task-{taskNumber}",
            Action = "updated",
            ProjectId = projectId,
            StateChanges = stateChanges.Count > 0 ? stateChanges : null
        });

        // Re-query with includes for full response
        var fullTask = await db.WorkPackageTasks
            .Include(t => t.Phase)
            .Include(t => t.BlockedBy).ThenInclude(d => d.DependsOnTask)
            .Include(t => t.Blocking).ThenInclude(d => d.DependentTask)
            .FirstAsync(t => t.Id == task.Id, ct);

        var response = ToResponse(fullTask, wp, fullTask.Phase);
        response.StateChanges = stateChanges.Count > 0 ? stateChanges : null;
        return response;
    }

    public async Task<BatchUpdateTaskStatesResponse?> BatchUpdateStatesAsync(
        long projectId, int wpNumber, BatchUpdateTaskStatesRequest request, string changedBy, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default)
    {
        stateChanges ??= [];

        var wp = await db.WorkPackages
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct);

        if (wp is null)
            return null;

        var taskNumbers = request.Tasks.Select(t => t.TaskNumber).ToList();

        var tasks = await db.WorkPackageTasks
            .Include(t => t.Phase)
            .Include(t => t.BlockedBy).ThenInclude(d => d.DependsOnTask)
            .Include(t => t.Blocking).ThenInclude(d => d.DependentTask)
            .Where(t => t.WorkPackageId == wp.Id && taskNumbers.Contains(t.TaskNumber))
            .ToListAsync(ct);

        var taskMap = tasks.ToDictionary(t => t.TaskNumber);
        var auditEntries = new List<TaskAuditLog>();
        var now = DateTimeOffset.UtcNow;
        var results = new List<TaskStateResult>();
        var affectedPhaseIds = new HashSet<long>();
        var terminalTasks = new List<WorkPackageTask>();

        foreach (var update in request.Tasks)
        {
            if (!taskMap.TryGetValue(update.TaskNumber, out var task))
                continue;

            var oldState = task.State;
            if (oldState == update.State)
            {
                results.Add(new TaskStateResult
                {
                    TaskId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{task.TaskNumber}",
                    OldState = oldState.ToString(),
                    NewState = update.State.ToString()
                });
                continue;
            }

            // Audit the state change
            auditEntries.Add(new TaskAuditLog
            {
                TaskId = task.Id,
                FieldName = "State",
                OldValue = oldState.ToString(),
                NewValue = update.State.ToString(),
                ChangedBy = changedBy,
                ChangedAt = now
            });

            task.State = update.State;
            StateTransitionHelper.ApplyBlockedStateLogic(task, oldState, update.State);
            StateTransitionHelper.ApplyStateTimestamps(task, oldState, update.State);

            affectedPhaseIds.Add(task.PhaseId);

            if (CompletionStateConstants.TerminalStates.Contains(update.State))
                terminalTasks.Add(task);

            results.Add(new TaskStateResult
            {
                TaskId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{task.TaskNumber}",
                OldState = oldState.ToString(),
                NewState = update.State.ToString()
            });
        }

        if (auditEntries.Count > 0)
            db.TaskAuditLogs.AddRange(auditEntries);

        // Run cascades after all state changes are applied
        foreach (var task in terminalTasks)
            await cascadeService.AutoUnblockDependentTasksAsync(task, wp, stateChanges, ct);

        foreach (var phaseId in affectedPhaseIds)
            await cascadeService.PropagateStateUpwardAsync(phaseId, wp, changedBy, stateChanges, ct);

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "Task",
            EntityId = $"proj-{projectId}-wp-{wpNumber}",
            Action = "batch-updated",
            ProjectId = projectId,
            StateChanges = stateChanges.Count > 0 ? stateChanges : null
        });

        return new BatchUpdateTaskStatesResponse
        {
            UpdatedCount = results.Count(r => r.OldState != r.NewState),
            Results = results,
            StateChanges = stateChanges.Count > 0 ? stateChanges : null
        };
    }

    public async Task<bool> DeleteAsync(
        long projectId, int wpNumber, int taskNumber, CancellationToken ct = default)
    {
        var wp = await db.WorkPackages
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct);

        if (wp is null)
            return false;

        var task = await db.WorkPackageTasks
            .FirstOrDefaultAsync(t => t.WorkPackageId == wp.Id && t.TaskNumber == taskNumber, ct);

        if (task is null)
            return false;

        db.WorkPackageTasks.Remove(task);
        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "Task",
            EntityId = $"proj-{projectId}-wp-{wpNumber}-task-{task.TaskNumber}",
            Action = "deleted",
            ProjectId = projectId
        });

        return true;
    }

    public async Task<TaskDependencyResponse> AddDependencyAsync(
        long projectId, int wpNumber, int taskNumber, ManageDependencyRequest request, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default)
    {
        stateChanges ??= [];

        var wp = await db.WorkPackages
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct)
            ?? throw new KeyNotFoundException($"Work package {wpNumber} not found in project {projectId}");

        var dependentTask = await db.WorkPackageTasks
            .FirstOrDefaultAsync(t => t.WorkPackageId == wp.Id && t.TaskNumber == taskNumber, ct)
            ?? throw new KeyNotFoundException($"Task {taskNumber} not found in work package {wpNumber}");

        var dependsOnTask = await db.WorkPackageTasks
            .FirstOrDefaultAsync(t => t.Id == request.DependsOnId, ct)
            ?? throw new KeyNotFoundException($"Dependency target task {request.DependsOnId} not found");

        // Validate: no self-dependency
        if (dependentTask.Id == dependsOnTask.Id)
            throw new InvalidOperationException("A task cannot depend on itself.");

        // Validate: no duplicate
        var exists = await db.WorkPackageTaskDependencies
            .AnyAsync(d => d.DependentTaskId == dependentTask.Id && d.DependsOnTaskId == dependsOnTask.Id, ct);
        if (exists)
            throw new InvalidOperationException("This dependency already exists.");

        // Validate: no circular dependency
        if (await cascadeService.HasCircularTaskDependencyAsync(dependentTask.Id, dependsOnTask.Id, ct))
            throw new InvalidOperationException("Adding this dependency would create a circular dependency.");

        var dependency = new WorkPackageTaskDependency
        {
            DependentTaskId = dependentTask.Id,
            DependsOnTaskId = dependsOnTask.Id,
            Reason = request.Reason
        };

        db.WorkPackageTaskDependencies.Add(dependency);

        // Auto-block: if dependent task is in an active state and depends-on is non-terminal, transition to Blocked
        if (!CompletionStateConstants.TerminalStates.Contains(dependsOnTask.State)
            && dependentTask.State != CompletionState.Blocked
            && !CompletionStateConstants.TerminalStates.Contains(dependentTask.State)
            && !CompletionStateConstants.InactiveStates.Contains(dependentTask.State))
        {
            var oldState = dependentTask.State;
            dependentTask.PreviousActiveState = oldState;
            dependentTask.State = CompletionState.Blocked;
            StateTransitionHelper.ApplyStateTimestamps(dependentTask, oldState, CompletionState.Blocked);

            var now = DateTimeOffset.UtcNow;
            db.TaskAuditLogs.Add(new TaskAuditLog
            {
                Task = dependentTask,
                FieldName = "State",
                OldValue = oldState.ToString(),
                NewValue = CompletionState.Blocked.ToString(),
                ChangedBy = "system",
                ChangedAt = now
            });

            stateChanges?.Add(new StateChangeDto
            {
                EntityType = "Task",
                EntityId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{dependentTask.TaskNumber}",
                OldState = oldState.ToString(),
                NewState = CompletionState.Blocked.ToString(),
                Reason = $"Auto-blocked: dependency on 'proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{dependsOnTask.TaskNumber}' added"
            });
        }

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "Task",
            EntityId = $"proj-{projectId}-wp-{wpNumber}-task-{taskNumber}",
            Action = "updated",
            ProjectId = projectId,
            StateChanges = stateChanges.Count > 0 ? stateChanges : null
        });

        var response = new TaskDependencyResponse
        {
            TaskId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{dependsOnTask.TaskNumber}",
            Name = dependsOnTask.Name,
            State = dependsOnTask.State.ToString(),
            Reason = dependency.Reason,
            StateChanges = stateChanges.Count > 0 ? stateChanges : null
        };
        return response;
    }

    public async Task<bool> RemoveDependencyAsync(
        long projectId, int wpNumber, int taskNumber, long dependsOnTaskId, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default)
    {
        stateChanges ??= [];

        var wp = await db.WorkPackages
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct);

        if (wp is null)
            return false;

        var dependentTask = await db.WorkPackageTasks
            .FirstOrDefaultAsync(t => t.WorkPackageId == wp.Id && t.TaskNumber == taskNumber, ct);

        if (dependentTask is null)
            return false;

        var dependency = await db.WorkPackageTaskDependencies
            .FirstOrDefaultAsync(d => d.DependentTaskId == dependentTask.Id && d.DependsOnTaskId == dependsOnTaskId, ct);

        if (dependency is null)
            return false;

        db.WorkPackageTaskDependencies.Remove(dependency);

        // After removal: if dependent task is Blocked, check remaining non-terminal blockers
        if (dependentTask.State == CompletionState.Blocked)
        {
            var remainingBlockers = await db.WorkPackageTaskDependencies
                .Where(d => d.DependentTaskId == dependentTask.Id && d.DependsOnTaskId != dependsOnTaskId)
                .Include(d => d.DependsOnTask)
                .AnyAsync(d => !CompletionStateConstants.TerminalStates.Contains(d.DependsOnTask.State), ct);

            if (!remainingBlockers && dependentTask.PreviousActiveState is not null)
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
                    Reason = "Auto-unblocked: no remaining blockers"
                });
            }
        }

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "Task",
            EntityId = $"proj-{projectId}-wp-{wpNumber}-task-{taskNumber}",
            Action = "updated",
            ProjectId = projectId,
            StateChanges = stateChanges.Count > 0 ? stateChanges : null
        });

        return true;
    }

    // ── Private helpers ──

    private static List<TaskAuditLog> BuildCreateAuditEntries(WorkPackageTask task, string changedBy)
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new List<TaskAuditLog>();

        void Add(string field, string? value)
        {
            if (value is null) return;
            entries.Add(new TaskAuditLog
            {
                Task = task,
                FieldName = field,
                OldValue = null,
                NewValue = value,
                ChangedBy = changedBy,
                ChangedAt = now
            });
        }

        Add("Name", task.Name);
        Add("Description", task.Description);
        Add("SortOrder", task.SortOrder.ToString());
        Add("ImplementationNotes", task.ImplementationNotes);
        Add("State", task.State.ToString());

        if (task.TargetFiles.Count > 0)
            Add("TargetFiles", JsonSerializer.Serialize(task.TargetFiles.Select(f => new { f.FileName, f.RelativePath, f.Description })));

        if (task.Attachments.Count > 0)
            Add("Attachments", JsonSerializer.Serialize(task.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description })));

        return entries;
    }

    private static void AuditAndSet(
        List<TaskAuditLog> entries, long taskId, string changedBy, DateTimeOffset now,
        string field, string? oldValue, string newValue, Action<string> setter)
    {
        if (oldValue == newValue) return;
        entries.Add(new TaskAuditLog
        {
            TaskId = taskId,
            FieldName = field,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = changedBy,
            ChangedAt = now
        });
        setter(newValue);
    }

    private static void AuditAndSetEnum<TEnum>(
        List<TaskAuditLog> entries, long taskId, string changedBy, DateTimeOffset now,
        string field, TEnum oldValue, TEnum newValue, Action<TEnum> setter) where TEnum : struct, Enum
    {
        if (EqualityComparer<TEnum>.Default.Equals(oldValue, newValue)) return;
        entries.Add(new TaskAuditLog
        {
            TaskId = taskId,
            FieldName = field,
            OldValue = oldValue.ToString(),
            NewValue = newValue.ToString(),
            ChangedBy = changedBy,
            ChangedAt = now
        });
        setter(newValue);
    }

    private static void AuditAndSetInt(
        List<TaskAuditLog> entries, long taskId, string changedBy, DateTimeOffset now,
        string field, int oldValue, int newValue, Action<int> setter)
    {
        if (oldValue == newValue) return;
        entries.Add(new TaskAuditLog
        {
            TaskId = taskId,
            FieldName = field,
            OldValue = oldValue.ToString(),
            NewValue = newValue.ToString(),
            ChangedBy = changedBy,
            ChangedAt = now
        });
        setter(newValue);
    }

    private static TaskResponse ToResponse(WorkPackageTask t, WorkPackage wp, WorkPackagePhase phase) =>
        ResponseMapper.MapTask(t, wp.ProjectId, wp.WorkPackageNumber, phase.PhaseNumber);
}
