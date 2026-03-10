using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class WorkPackageTaskService(AppDbContext db) : IWorkPackageTaskService
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
                TargetFiles = MapTargetFiles(request.TargetFiles),
                Attachments = MapAttachments(request.Attachments)
            };

            // Apply state-driven timestamps
            ApplyStateTimestamps(task, CompletionState.NotStarted, request.State);

            // Apply blocked state logic
            ApplyBlockedStateLogic(task, CompletionState.NotStarted, request.State);

            db.WorkPackageTasks.Add(task);

            // Audit all fields on creation
            var auditEntries = BuildCreateAuditEntries(task, changedBy);
            db.TaskAuditLogs.AddRange(auditEntries);

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);

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
            task.TargetFiles = MapTargetFiles(request.TargetFiles);
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
            task.Attachments = MapAttachments(request.Attachments);
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
            ApplyBlockedStateLogic(task, oldState, newState);
            ApplyStateTimestamps(task, oldState, newState);
        }

        if (auditEntries.Count > 0)
            db.TaskAuditLogs.AddRange(auditEntries);

        // Upward propagation check after state change
        if (request.State is not null && oldState != request.State.Value)
        {
            await PropagateStateUpwardAsync(task.PhaseId, wp, changedBy, stateChanges, ct);

            // If task was moved between phases, also check old phase
            if (oldPhaseId != task.PhaseId)
                await PropagateStateUpwardAsync(oldPhaseId, wp, changedBy, stateChanges, ct);

            // Dep-completion auto-unblock: if task transitioned to terminal, unblock dependent tasks
            if (CompletionStateConstants.TerminalStates.Contains(request.State.Value))
                await AutoUnblockDependentTasksAsync(task, wp, stateChanges, ct);
        }

        // If phase changed (without state change), check both old and new phase
        if (request.PhaseId is not null && request.PhaseId.Value != oldPhaseId && (request.State is null || oldState == request.State.Value))
        {
            await PropagateStateUpwardAsync(task.PhaseId, wp, changedBy, stateChanges, ct);
            await PropagateStateUpwardAsync(oldPhaseId, wp, changedBy, stateChanges, ct);
        }

        await db.SaveChangesAsync(ct);

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

        // Validate: no circular dependency (BFS from dependsOnTask through its BlockedBy chain)
        if (await HasCircularDependencyAsync(dependentTask.Id, dependsOnTask.Id, ct))
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
            ApplyStateTimestamps(dependentTask, oldState, CompletionState.Blocked);

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

                ApplyStateTimestamps(dependentTask, oldState, restoredState);

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
        return true;
    }

    // ── Private helpers ──

    private async Task PropagateStateUpwardAsync(long phaseId, WorkPackage wp, string changedBy, List<StateChangeDto>? stateChanges, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Check if all tasks in the phase are terminal
        var phaseTasks = await db.WorkPackageTasks
            .Where(t => t.PhaseId == phaseId)
            .ToListAsync(ct);

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

        // Check if all phases in WP are terminal
        var allPhases = await db.WorkPackagePhases
            .Where(p => p.WorkPackageId == wp.Id)
            .ToListAsync(ct);

        if (allPhases.Count > 0 && allPhases.All(p => CompletionStateConstants.TerminalStates.Contains(p.State)))
        {
            if (!CompletionStateConstants.TerminalStates.Contains(wp.State))
            {
                var oldWpState = wp.State;
                wp.State = CompletionState.Completed;

                if (wp.StartedAt is null)
                    wp.StartedAt = now;
                wp.CompletedAt = now;
                wp.ResolvedAt = now;

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

    private async Task AutoUnblockDependentTasksAsync(WorkPackageTask completedTask, WorkPackage wp, List<StateChangeDto>? stateChanges, CancellationToken ct)
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
                ApplyStateTimestamps(dependentTask, oldState, restoredState);

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

    private async Task<bool> HasCircularDependencyAsync(long dependentId, long dependsOnId, CancellationToken ct)
    {
        // BFS from dependsOnTask through its BlockedBy chain, checking if we reach dependentId
        var visited = new HashSet<long>();
        var queue = new Queue<long>();
        queue.Enqueue(dependsOnId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (currentId == dependentId)
                return true;

            if (!visited.Add(currentId))
                continue;

            var blockedByIds = await db.WorkPackageTaskDependencies
                .Where(d => d.DependentTaskId == currentId)
                .Select(d => d.DependsOnTaskId)
                .ToListAsync(ct);

            foreach (var id in blockedByIds)
                queue.Enqueue(id);
        }

        return false;
    }

    private static void ApplyStateTimestamps(WorkPackageTask task, CompletionState oldState, CompletionState newState)
    {
        if (oldState == newState)
            return;

        var now = DateTimeOffset.UtcNow;

        // StartedAt: set once when entering an active state from inactive
        if (task.StartedAt is null && CompletionStateConstants.ActiveStates.Contains(newState))
            task.StartedAt = now;

        // CompletedAt: set when entering Completed, cleared when leaving terminal
        if (newState == CompletionState.Completed)
            task.CompletedAt = now;
        else if (CompletionStateConstants.TerminalStates.Contains(oldState) && !CompletionStateConstants.TerminalStates.Contains(newState))
            task.CompletedAt = null;

        // ResolvedAt: set when entering any terminal state, cleared when leaving terminal
        if (CompletionStateConstants.TerminalStates.Contains(newState))
            task.ResolvedAt = now;
        else if (CompletionStateConstants.TerminalStates.Contains(oldState))
            task.ResolvedAt = null;
    }

    private static void ApplyBlockedStateLogic(WorkPackageTask task, CompletionState oldState, CompletionState newState)
    {
        // Transitioning TO Blocked: capture previous active state
        if (newState == CompletionState.Blocked && CompletionStateConstants.ActiveStates.Contains(oldState))
            task.PreviousActiveState = oldState;

        // Transitioning FROM Blocked: clear previous active state
        if (oldState == CompletionState.Blocked && newState != CompletionState.Blocked)
            task.PreviousActiveState = null;
    }

    private static List<FileReference> MapAttachments(List<FileReferenceDto>? dtos)
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

    private static List<FileReference> MapTargetFiles(List<FileReferenceDto>? dtos)
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

    private static TaskResponse ToResponse(WorkPackageTask t, WorkPackage wp, WorkPackagePhase phase) => new()
    {
        TaskId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{t.TaskNumber}",
        Id = t.Id,
        TaskNumber = t.TaskNumber,
        PhaseId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-phase-{phase.PhaseNumber}",
        Name = t.Name,
        Description = t.Description,
        SortOrder = t.SortOrder,
        ImplementationNotes = t.ImplementationNotes,
        State = t.State.ToString(),
        PreviousActiveState = t.PreviousActiveState?.ToString(),
        StartedAt = t.StartedAt,
        CompletedAt = t.CompletedAt,
        ResolvedAt = t.ResolvedAt,
        TargetFiles = t.TargetFiles.Select(f => new FileReferenceDto
        {
            FileName = f.FileName,
            RelativePath = f.RelativePath,
            Description = f.Description
        }).ToList(),
        Attachments = t.Attachments.Select(a => new FileReferenceDto
        {
            FileName = a.FileName,
            RelativePath = a.RelativePath,
            Description = a.Description
        }).ToList(),
        BlockedBy = t.BlockedBy.Select(d => new TaskDependencyResponse
        {
            TaskId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{d.DependsOnTask.TaskNumber}",
            Name = d.DependsOnTask.Name,
            State = d.DependsOnTask.State.ToString(),
            Reason = d.Reason
        }).ToList(),
        Blocking = t.Blocking.Select(d => new TaskDependencyResponse
        {
            TaskId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{d.DependentTask.TaskNumber}",
            Name = d.DependentTask.Name,
            State = d.DependentTask.State.ToString(),
            Reason = d.Reason
        }).ToList(),
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
