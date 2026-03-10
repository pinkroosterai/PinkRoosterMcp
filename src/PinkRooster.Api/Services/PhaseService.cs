using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class PhaseService(AppDbContext db) : IPhaseService
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
                        TargetFiles = MapTargetFiles(taskReq.TargetFiles),
                        Attachments = MapAttachments(taskReq.Attachments)
                    };

                    // Apply state timestamps if initial state is active
                    ApplyStateTimestamps(task, CompletionState.NotStarted, taskReq.State);

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
                        existingTask.TargetFiles = MapTargetFiles(taskDto.TargetFiles);
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
                        existingTask.Attachments = MapAttachments(taskDto.Attachments);
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

                    // Apply state timestamps if state changed
                    if (taskDto.State is not null && oldTaskState != taskDto.State.Value)
                        ApplyStateTimestamps(existingTask, oldTaskState, taskDto.State.Value);
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
                        TargetFiles = MapTargetFiles(taskDto.TargetFiles),
                        Attachments = MapAttachments(taskDto.Attachments)
                    };

                    // Apply state timestamps
                    ApplyStateTimestamps(newTask, CompletionState.NotStarted, newTask.State);

                    db.WorkPackageTasks.Add(newTask);

                    // Build create audit entries
                    var createEntries = BuildTaskCreateAuditEntries(newTask, changedBy);
                    db.TaskAuditLogs.AddRange(createEntries);
                }
            }
        }

        // Upward propagation: check if all tasks in this phase are terminal
        // Reload tasks to get current state after modifications
        var allPhaseTasks = await db.WorkPackageTasks
            .Where(t => t.PhaseId == phase.Id)
            .ToListAsync(ct);

        if (allPhaseTasks.Count > 0 &&
            allPhaseTasks.All(t => CompletionStateConstants.TerminalStates.Contains(t.State)) &&
            !CompletionStateConstants.TerminalStates.Contains(phase.State))
        {
            var oldPhaseState = phase.State;
            phase.State = CompletionState.Completed;

            phaseAuditEntries.Add(new PhaseAuditLog
            {
                PhaseId = phase.Id,
                FieldName = "State",
                OldValue = oldPhaseState.ToString(),
                NewValue = CompletionState.Completed.ToString(),
                ChangedBy = "system",
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

            // Check if all phases in WP are terminal -> auto-complete WP
            var allWpPhases = await db.WorkPackagePhases
                .Where(p => p.WorkPackageId == wp.Id)
                .ToListAsync(ct);

            if (allWpPhases.Count > 0 &&
                allWpPhases.All(p => CompletionStateConstants.TerminalStates.Contains(p.State)) &&
                !CompletionStateConstants.TerminalStates.Contains(wp.State))
            {
                var oldWpState = wp.State;
                wp.State = CompletionState.Completed;

                // Apply WP state timestamps
                ApplyWpStateTimestamps(wp, oldWpState, CompletionState.Completed);

                db.WorkPackageAuditLogs.Add(new WorkPackageAuditLog
                {
                    WorkPackageId = wp.Id,
                    FieldName = "State",
                    OldValue = oldWpState.ToString(),
                    NewValue = CompletionState.Completed.ToString(),
                    ChangedBy = "system",
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

        if (phaseAuditEntries.Count > 0)
            db.PhaseAuditLogs.AddRange(phaseAuditEntries);

        if (taskAuditEntries.Count > 0)
            db.TaskAuditLogs.AddRange(taskAuditEntries);

        await db.SaveChangesAsync(ct);

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

        db.WorkPackagePhases.Remove(phase);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ── Private helpers ──

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

    private static void ApplyWpStateTimestamps(WorkPackage wp, CompletionState oldState, CompletionState newState)
    {
        if (oldState == newState)
            return;

        var now = DateTimeOffset.UtcNow;

        if (wp.StartedAt is null && CompletionStateConstants.ActiveStates.Contains(newState))
            wp.StartedAt = now;

        if (newState == CompletionState.Completed)
            wp.CompletedAt = now;
        else if (CompletionStateConstants.TerminalStates.Contains(oldState) && !CompletionStateConstants.TerminalStates.Contains(newState))
            wp.CompletedAt = null;

        if (CompletionStateConstants.TerminalStates.Contains(newState))
            wp.ResolvedAt = now;
        else if (CompletionStateConstants.TerminalStates.Contains(oldState))
            wp.ResolvedAt = null;
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

    private static PhaseResponse ToResponse(WorkPackagePhase p)
    {
        var wp = p.WorkPackage;
        return new PhaseResponse
        {
            PhaseId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-phase-{p.PhaseNumber}",
            Id = p.Id,
            PhaseNumber = p.PhaseNumber,
            Name = p.Name,
            Description = p.Description,
            SortOrder = p.SortOrder,
            State = p.State.ToString(),
            Tasks = p.Tasks.Select(t => new TaskResponse
            {
                TaskId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-task-{t.TaskNumber}",
                Id = t.Id,
                TaskNumber = t.TaskNumber,
                PhaseId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}-phase-{p.PhaseNumber}",
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
                Attachments = t.Attachments.Select(f => new FileReferenceDto
                {
                    FileName = f.FileName,
                    RelativePath = f.RelativePath,
                    Description = f.Description
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
            }).ToList(),
            AcceptanceCriteria = p.AcceptanceCriteria.Select(ac => new AcceptanceCriterionDto
            {
                Name = ac.Name,
                Description = ac.Description,
                VerificationMethod = ac.VerificationMethod,
                VerificationResult = ac.VerificationResult,
                VerifiedAt = ac.VerifiedAt
            }).ToList(),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
