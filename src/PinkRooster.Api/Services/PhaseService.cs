using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class PhaseService(AppDbContext db, IStateCascadeService cascadeService, IEventBroadcaster broadcaster) : IPhaseService
{
    public async Task<PhaseResponse> CreateAsync(
        long projectId, int wpNumber, CreatePhaseRequest request, string changedBy, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async (cancellation) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellation);

            var wp = await db.WorkPackages
                .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, cancellation)
                ?? throw new InvalidOperationException($"Work package {wpNumber} not found in project {projectId}");

            // Assign PhaseNumber: MAX(phase_number) + 1 within WP
            var nextPhaseNumber = await db.WorkPackagePhases
                .Where(p => p.WorkPackageId == wp.Id)
                .MaxAsync(p => (int?)p.PhaseNumber, cancellation) ?? 0;
            nextPhaseNumber++;

            // Auto-assign SortOrder if not provided
            var sortOrder = request.SortOrder
                ?? (await db.WorkPackagePhases
                    .Where(p => p.WorkPackageId == wp.Id)
                    .MaxAsync(p => (int?)p.SortOrder, cancellation) ?? 0) + 1;

            var phase = new WorkPackagePhase
            {
                PhaseNumber = nextPhaseNumber,
                WorkPackageId = wp.Id,
                Name = request.Name,
                Description = request.Description,
                SortOrder = sortOrder,
                State = CompletionState.NotStarted
            };

            db.WorkPackagePhases.Add(phase);

            // Create AcceptanceCriteria if provided
            if (request.AcceptanceCriteria is { Count: > 0 })
            {
                foreach (var acDto in request.AcceptanceCriteria)
                {
                    db.AcceptanceCriteria.Add(new AcceptanceCriterion
                    {
                        Phase = phase,
                        Name = acDto.Name,
                        Description = acDto.Description,
                        VerificationMethod = acDto.VerificationMethod,
                        VerificationResult = acDto.VerificationResult,
                        VerifiedAt = acDto.VerifiedAt
                    });
                }
            }

            // Create Tasks if provided
            if (request.Tasks is { Count: > 0 })
            {
                // Fetch starting numbers once before the loop to avoid duplicate key issues in batch
                var nextTaskNumber = (await db.WorkPackageTasks
                    .Where(t => t.WorkPackageId == wp.Id)
                    .MaxAsync(t => (int?)t.TaskNumber, cancellation) ?? 0) + 1;

                var nextSortOrder = (await db.WorkPackageTasks
                    .Where(t => t.WorkPackageId == wp.Id)
                    .MaxAsync(t => (int?)t.SortOrder, cancellation) ?? 0) + 1;

                foreach (var taskReq in request.Tasks)
                {
                    var taskSortOrder = taskReq.SortOrder ?? nextSortOrder++;

                    var task = new WorkPackageTask
                    {
                        TaskNumber = nextTaskNumber++,
                        Phase = phase,
                        WorkPackageId = wp.Id,
                        Name = taskReq.Name,
                        Description = taskReq.Description,
                        SortOrder = taskSortOrder,
                        ImplementationNotes = taskReq.ImplementationNotes,
                        State = taskReq.State,
                        TargetFiles = StateTransitionHelper.MapFileReferences(taskReq.TargetFiles),
                        Attachments = StateTransitionHelper.MapFileReferences(taskReq.Attachments)
                    };

                    // Apply state timestamps if initial state is active
                    StateTransitionHelper.ApplyStateTimestamps(task, CompletionState.NotStarted, taskReq.State);

                    db.WorkPackageTasks.Add(task);

                    // Build audit entries for each task
                    var taskAuditEntries = BuildTaskCreateAuditEntries(task, changedBy);
                    db.TaskAuditLogs.AddRange(taskAuditEntries);
                }
            }

            // Build phase audit entries
            var phaseAuditEntries = BuildPhaseCreateAuditEntries(phase, changedBy);
            db.PhaseAuditLogs.AddRange(phaseAuditEntries);

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);

            broadcaster.Publish(new ServerEvent
            {
                EventType = "entity:changed",
                EntityType = "Phase",
                EntityId = $"proj-{projectId}-wp-{wpNumber}-phase-{phase.PhaseNumber}",
                Action = "created",
                ProjectId = projectId
            });

            // Re-query to get full tree including tasks with dependencies
            var fullPhase = await db.WorkPackagePhases
                .Include(p => p.Tasks.OrderBy(t => t.SortOrder))
                    .ThenInclude(t => t.BlockedBy).ThenInclude(d => d.DependsOnTask)
                .Include(p => p.Tasks)
                    .ThenInclude(t => t.Blocking).ThenInclude(d => d.DependentTask)
                .Include(p => p.AcceptanceCriteria)
                .Include(p => p.WorkPackage)
                .FirstAsync(p => p.Id == phase.Id, cancellation);

            return ToResponse(fullPhase);
        }, ct);
    }

    public async Task<PhaseResponse?> UpdateAsync(
        long projectId, int wpNumber, int phaseNumber, UpdatePhaseRequest request, string changedBy, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default)
    {
        stateChanges ??= [];

        var phase = await db.WorkPackagePhases
            .Include(p => p.WorkPackage)
            .Include(p => p.Tasks.OrderBy(t => t.SortOrder))
            .Include(p => p.AcceptanceCriteria)
            .FirstOrDefaultAsync(p =>
                p.WorkPackage.ProjectId == projectId &&
                p.WorkPackage.WorkPackageNumber == wpNumber &&
                p.PhaseNumber == phaseNumber, ct);

        if (phase is null)
            return null;

        var wp = phase.WorkPackage;
        var phaseAuditEntries = new List<PhaseAuditLog>();
        var taskAuditEntries = new List<TaskAuditLog>();
        var now = DateTimeOffset.UtcNow;

        // Per-field audit for phase fields
        if (request.Name is not null)
            PhaseAuditAndSet(phaseAuditEntries, phase.Id, changedBy, now, "Name", phase.Name, request.Name, v => phase.Name = v);

        if (request.Description is not null)
            PhaseAuditAndSet(phaseAuditEntries, phase.Id, changedBy, now, "Description", phase.Description, request.Description, v => phase.Description = v);

        if (request.SortOrder is not null)
        {
            var oldSortOrder = phase.SortOrder;
            var newSortOrder = request.SortOrder.Value;
            if (oldSortOrder != newSortOrder)
            {
                phaseAuditEntries.Add(new PhaseAuditLog
                {
                    PhaseId = phase.Id,
                    FieldName = "SortOrder",
                    OldValue = oldSortOrder.ToString(),
                    NewValue = newSortOrder.ToString(),
                    ChangedBy = changedBy,
                    ChangedAt = now
                });
                phase.SortOrder = newSortOrder;
            }
        }

        if (request.State is not null)
            PhaseAuditAndSetEnum(phaseAuditEntries, phase.Id, changedBy, now, "State", phase.State, request.State.Value, v => phase.State = v);

        // AcceptanceCriteria: full replacement if provided
        if (request.AcceptanceCriteria is not null)
        {
            db.AcceptanceCriteria.RemoveRange(phase.AcceptanceCriteria);
            phase.AcceptanceCriteria.Clear();

            foreach (var acDto in request.AcceptanceCriteria)
            {
                phase.AcceptanceCriteria.Add(new AcceptanceCriterion
                {
                    Phase = phase,
                    Name = acDto.Name,
                    Description = acDto.Description,
                    VerificationMethod = acDto.VerificationMethod,
                    VerificationResult = acDto.VerificationResult,
                    VerifiedAt = acDto.VerifiedAt
                });
            }
        }

        // Tasks: upsert logic
        var tasksReachingTerminal = new List<WorkPackageTask>();
        var affectedPhaseIds = new HashSet<long> { phase.Id };

        if (request.Tasks is { Count: > 0 })
        {
            // Pre-fetch starting numbers for new tasks to avoid duplicate key issues in batch
            var nextNewTaskNumber = (await db.WorkPackageTasks
                .Where(t => t.WorkPackageId == wp.Id)
                .MaxAsync(t => (int?)t.TaskNumber, ct) ?? 0) + 1;

            var nextNewSortOrder = (await db.WorkPackageTasks
                .Where(t => t.WorkPackageId == wp.Id)
                .MaxAsync(t => (int?)t.SortOrder, ct) ?? 0) + 1;

            foreach (var taskDto in request.Tasks)
            {
                if (taskDto.TaskNumber is not null)
                {
                    // Update existing task
                    var existingTask = await db.WorkPackageTasks
                        .FirstOrDefaultAsync(t => t.WorkPackageId == wp.Id && t.TaskNumber == taskDto.TaskNumber.Value, ct);

                    if (existingTask is null)
                        continue;

                    var oldTaskState = existingTask.State;

                    if (taskDto.Name is not null)
                        TaskAuditAndSet(taskAuditEntries, existingTask.Id, changedBy, now, "Name", existingTask.Name, taskDto.Name, v => existingTask.Name = v);

                    if (taskDto.Description is not null)
                        TaskAuditAndSet(taskAuditEntries, existingTask.Id, changedBy, now, "Description", existingTask.Description, taskDto.Description, v => existingTask.Description = v);

                    if (taskDto.SortOrder is not null)
                    {
                        var oldSort = existingTask.SortOrder;
                        var newSort = taskDto.SortOrder.Value;
                        if (oldSort != newSort)
                        {
                            taskAuditEntries.Add(new TaskAuditLog
                            {
                                TaskId = existingTask.Id,
                                FieldName = "SortOrder",
                                OldValue = oldSort.ToString(),
                                NewValue = newSort.ToString(),
                                ChangedBy = changedBy,
                                ChangedAt = now
                            });
                            existingTask.SortOrder = newSort;
                        }
                    }

                    if (taskDto.ImplementationNotes is not null)
                        TaskAuditAndSet(taskAuditEntries, existingTask.Id, changedBy, now, "ImplementationNotes", existingTask.ImplementationNotes, taskDto.ImplementationNotes, v => existingTask.ImplementationNotes = v);

                    if (taskDto.State is not null)
                        TaskAuditAndSetEnum(taskAuditEntries, existingTask.Id, changedBy, now, "State", existingTask.State, taskDto.State.Value, v => existingTask.State = v);

                    if (taskDto.TargetFiles is not null)
                    {
                        var oldJson = JsonSerializer.Serialize(existingTask.TargetFiles.Select(f => new { f.FileName, f.RelativePath, f.Description }));
                        existingTask.TargetFiles = StateTransitionHelper.MapFileReferences(taskDto.TargetFiles);
                        var newJson = JsonSerializer.Serialize(existingTask.TargetFiles.Select(f => new { f.FileName, f.RelativePath, f.Description }));
                        if (oldJson != newJson)
                        {
                            taskAuditEntries.Add(new TaskAuditLog
                            {
                                TaskId = existingTask.Id,
                                FieldName = "TargetFiles",
                                OldValue = oldJson,
                                NewValue = newJson,
                                ChangedBy = changedBy,
                                ChangedAt = now
                            });
                        }
                    }

                    if (taskDto.Attachments is not null)
                    {
                        var oldJson = JsonSerializer.Serialize(existingTask.Attachments.Select(f => new { f.FileName, f.RelativePath, f.Description }));
                        existingTask.Attachments = StateTransitionHelper.MapFileReferences(taskDto.Attachments);
                        var newJson = JsonSerializer.Serialize(existingTask.Attachments.Select(f => new { f.FileName, f.RelativePath, f.Description }));
                        if (oldJson != newJson)
                        {
                            taskAuditEntries.Add(new TaskAuditLog
                            {
                                TaskId = existingTask.Id,
                                FieldName = "Attachments",
                                OldValue = oldJson,
                                NewValue = newJson,
                                ChangedBy = changedBy,
                                ChangedAt = now
                            });
                        }
                    }

                    // Apply blocked state logic and state timestamps if state changed
                    if (taskDto.State is not null && oldTaskState != taskDto.State.Value)
                    {
                        StateTransitionHelper.ApplyBlockedStateLogic(existingTask, oldTaskState, taskDto.State.Value);
                        StateTransitionHelper.ApplyStateTimestamps(existingTask, oldTaskState, taskDto.State.Value);

                        // Track for cascade processing after the loop
                        affectedPhaseIds.Add(existingTask.PhaseId);
                        if (CompletionStateConstants.TerminalStates.Contains(taskDto.State.Value))
                            tasksReachingTerminal.Add(existingTask);
                    }
                }
                else
                {
                    // Create new task
                    var taskSortOrder = taskDto.SortOrder ?? nextNewSortOrder++;

                    var newTask = new WorkPackageTask
                    {
                        TaskNumber = nextNewTaskNumber++,
                        Phase = phase,
                        WorkPackageId = wp.Id,
                        Name = taskDto.Name ?? throw new InvalidOperationException("Name is required for new tasks"),
                        Description = taskDto.Description ?? throw new InvalidOperationException("Description is required for new tasks"),
                        SortOrder = taskSortOrder,
                        ImplementationNotes = taskDto.ImplementationNotes,
                        State = taskDto.State ?? CompletionState.NotStarted,
                        TargetFiles = StateTransitionHelper.MapFileReferences(taskDto.TargetFiles),
                        Attachments = StateTransitionHelper.MapFileReferences(taskDto.Attachments)
                    };

                    // Apply state timestamps and blocked state logic
                    StateTransitionHelper.ApplyStateTimestamps(newTask, CompletionState.NotStarted, newTask.State);
                    StateTransitionHelper.ApplyBlockedStateLogic(newTask, CompletionState.NotStarted, newTask.State);

                    db.WorkPackageTasks.Add(newTask);

                    // Build create audit entries
                    var createEntries = BuildTaskCreateAuditEntries(newTask, changedBy);
                    db.TaskAuditLogs.AddRange(createEntries);
                }
            }
        }

        // Auto-unblock dependent tasks for any task that reached terminal state
        foreach (var terminalTask in tasksReachingTerminal)
            await cascadeService.AutoUnblockDependentTasksAsync(terminalTask, wp, stateChanges, ct);

        // Propagate upward for all affected phases (current phase + any phase with task state changes)
        foreach (var affectedPhaseId in affectedPhaseIds)
            await cascadeService.PropagateStateUpwardAsync(affectedPhaseId, wp, changedBy, stateChanges, ct);

        if (phaseAuditEntries.Count > 0)
            db.PhaseAuditLogs.AddRange(phaseAuditEntries);

        if (taskAuditEntries.Count > 0)
            db.TaskAuditLogs.AddRange(taskAuditEntries);

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "Phase",
            EntityId = $"proj-{projectId}-wp-{wpNumber}-phase-{phaseNumber}",
            Action = "updated",
            ProjectId = projectId,
            StateChanges = stateChanges.Count > 0 ? stateChanges : null
        });

        // Re-query with full tree for response
        var fullPhase = await db.WorkPackagePhases
            .Include(p => p.Tasks.OrderBy(t => t.SortOrder))
                .ThenInclude(t => t.BlockedBy).ThenInclude(d => d.DependsOnTask)
            .Include(p => p.Tasks)
                .ThenInclude(t => t.Blocking).ThenInclude(d => d.DependentTask)
            .Include(p => p.AcceptanceCriteria)
            .Include(p => p.WorkPackage)
            .FirstAsync(p => p.Id == phase.Id, ct);

        var response = ToResponse(fullPhase);
        response.StateChanges = stateChanges.Count > 0 ? stateChanges : null;
        return response;
    }

    public async Task<bool> DeleteAsync(long projectId, int wpNumber, int phaseNumber, CancellationToken ct = default)
    {
        var phase = await db.WorkPackagePhases
            .Include(p => p.WorkPackage)
            .FirstOrDefaultAsync(p =>
                p.WorkPackage.ProjectId == projectId &&
                p.WorkPackage.WorkPackageNumber == wpNumber &&
                p.PhaseNumber == phaseNumber, ct);

        if (phase is null)
            return false;

        var phaseProjectId = phase.WorkPackage.ProjectId;
        var phaseWpNumber = phase.WorkPackage.WorkPackageNumber;

        db.WorkPackagePhases.Remove(phase);
        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "Phase",
            EntityId = $"proj-{phaseProjectId}-wp-{phaseWpNumber}-phase-{phase.PhaseNumber}",
            Action = "deleted",
            ProjectId = phaseProjectId
        });

        return true;
    }

    public async Task<PhaseResponse?> VerifyAcceptanceCriteriaAsync(
        long projectId, int wpNumber, int phaseNumber, VerifyAcceptanceCriteriaRequest request, string changedBy, CancellationToken ct = default)
    {
        var phase = await db.WorkPackagePhases
            .Include(p => p.WorkPackage)
            .Include(p => p.AcceptanceCriteria)
            .Include(p => p.Tasks.OrderBy(t => t.SortOrder))
                .ThenInclude(t => t.BlockedBy).ThenInclude(d => d.DependsOnTask)
            .Include(p => p.Tasks)
                .ThenInclude(t => t.Blocking).ThenInclude(d => d.DependentTask)
            .FirstOrDefaultAsync(p =>
                p.WorkPackage.ProjectId == projectId &&
                p.WorkPackage.WorkPackageNumber == wpNumber &&
                p.PhaseNumber == phaseNumber, ct);

        if (phase is null)
            return null;

        var now = DateTimeOffset.UtcNow;
        var auditEntries = new List<PhaseAuditLog>();
        var matchedCount = 0;

        foreach (var item in request.Criteria)
        {
            var criterion = phase.AcceptanceCriteria
                .FirstOrDefault(ac => string.Equals(ac.Name, item.Name, StringComparison.OrdinalIgnoreCase));

            if (criterion is null)
                throw new InvalidOperationException($"Acceptance criterion '{item.Name}' not found on phase {phaseNumber}.");

            var oldResult = criterion.VerificationResult;
            var oldVerifiedAt = criterion.VerifiedAt;

            criterion.VerificationResult = item.VerificationResult;
            criterion.VerifiedAt = now;

            if (oldResult != item.VerificationResult)
            {
                auditEntries.Add(new PhaseAuditLog
                {
                    PhaseId = phase.Id,
                    FieldName = $"AcceptanceCriteria[{criterion.Name}].VerificationResult",
                    OldValue = oldResult,
                    NewValue = item.VerificationResult,
                    ChangedBy = changedBy,
                    ChangedAt = now
                });
            }

            if (oldVerifiedAt != now)
            {
                auditEntries.Add(new PhaseAuditLog
                {
                    PhaseId = phase.Id,
                    FieldName = $"AcceptanceCriteria[{criterion.Name}].VerifiedAt",
                    OldValue = oldVerifiedAt?.ToString("o"),
                    NewValue = now.ToString("o"),
                    ChangedBy = changedBy,
                    ChangedAt = now
                });
            }

            matchedCount++;
        }

        if (auditEntries.Count > 0)
            db.PhaseAuditLogs.AddRange(auditEntries);

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "Phase",
            EntityId = $"proj-{projectId}-wp-{wpNumber}-phase-{phaseNumber}",
            Action = "updated",
            ProjectId = projectId
        });

        return ToResponse(phase);
    }

    // ── Private helpers ──

    private static List<PhaseAuditLog> BuildPhaseCreateAuditEntries(WorkPackagePhase phase, string changedBy)
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new List<PhaseAuditLog>();

        void Add(string field, string? value)
        {
            if (value is null) return;
            entries.Add(new PhaseAuditLog
            {
                Phase = phase,
                FieldName = field,
                OldValue = null,
                NewValue = value,
                ChangedBy = changedBy,
                ChangedAt = now
            });
        }

        Add("Name", phase.Name);
        Add("Description", phase.Description);
        Add("SortOrder", phase.SortOrder.ToString());
        Add("State", phase.State.ToString());

        return entries;
    }

    private static List<TaskAuditLog> BuildTaskCreateAuditEntries(WorkPackageTask task, string changedBy)
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
            Add("Attachments", JsonSerializer.Serialize(task.Attachments.Select(f => new { f.FileName, f.RelativePath, f.Description })));

        return entries;
    }

    // ── Phase audit helpers ──

    private static void PhaseAuditAndSet(
        List<PhaseAuditLog> entries, long phaseId, string changedBy, DateTimeOffset now,
        string field, string? oldValue, string newValue, Action<string> setter)
    {
        if (oldValue == newValue) return;
        entries.Add(new PhaseAuditLog
        {
            PhaseId = phaseId,
            FieldName = field,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = changedBy,
            ChangedAt = now
        });
        setter(newValue);
    }

    private static void PhaseAuditAndSetEnum<TEnum>(
        List<PhaseAuditLog> entries, long phaseId, string changedBy, DateTimeOffset now,
        string field, TEnum oldValue, TEnum newValue, Action<TEnum> setter) where TEnum : struct, Enum
    {
        if (EqualityComparer<TEnum>.Default.Equals(oldValue, newValue)) return;
        entries.Add(new PhaseAuditLog
        {
            PhaseId = phaseId,
            FieldName = field,
            OldValue = oldValue.ToString(),
            NewValue = newValue.ToString(),
            ChangedBy = changedBy,
            ChangedAt = now
        });
        setter(newValue);
    }

    // ── Task audit helpers ──

    private static void TaskAuditAndSet(
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

    private static void TaskAuditAndSetEnum<TEnum>(
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

    // ── Response mapping ──

    private static PhaseResponse ToResponse(WorkPackagePhase p) =>
        ResponseMapper.MapPhase(p, p.WorkPackage.ProjectId, p.WorkPackage.WorkPackageNumber);
}
