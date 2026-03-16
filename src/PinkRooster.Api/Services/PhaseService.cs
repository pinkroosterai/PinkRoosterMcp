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

            var nextPhaseNumber = wp.NextPhaseNumber;
            wp.NextPhaseNumber++;

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
            AddAcceptanceCriteria(phase, request.AcceptanceCriteria);
            await CreateTasksForNewPhaseAsync(wp, phase, request.Tasks, changedBy, cancellation);
            AuditPhaseCreation(phase, changedBy);

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
                .ThenInclude(t => t.BlockedBy).ThenInclude(d => d.DependsOnTask)
            .Include(p => p.Tasks)
                .ThenInclude(t => t.Blocking).ThenInclude(d => d.DependentTask)
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
        var phaseAudit = () => new PhaseAuditLog { PhaseId = phase.Id, FieldName = default!, ChangedBy = changedBy, ChangedAt = now };
        var oldPhaseState = phase.State;

        AuditAndUpdatePhaseFields(phase, request, phaseAuditEntries, phaseAudit);
        await HandlePhaseStateCascadesAsync(phase, wp, request, oldPhaseState, changedBy, stateChanges, ct);
        ReplaceAcceptanceCriteria(phase, request.AcceptanceCriteria);

        var (tasksReachingTerminal, affectedPhaseIds) =
            await UpsertTasksAsync(phase, wp, request.Tasks, changedBy, now, taskAuditEntries, ct);

        await ProcessTaskStateCascadesAsync(tasksReachingTerminal, affectedPhaseIds, phase.Id, wp, changedBy, stateChanges, ct);

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

        var response = ToResponse(phase);
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

    // ── Shared helpers ──

    private static void AddAcceptanceCriteria(WorkPackagePhase phase, List<AcceptanceCriterionDto>? criteria)
    {
        if (criteria is not { Count: > 0 }) return;

        foreach (var acDto in criteria)
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

    private void ReplaceAcceptanceCriteria(WorkPackagePhase phase, List<AcceptanceCriterionDto>? criteria)
    {
        if (criteria is null) return;

        db.AcceptanceCriteria.RemoveRange(phase.AcceptanceCriteria);
        phase.AcceptanceCriteria.Clear();
        AddAcceptanceCriteria(phase, criteria);
    }

    private async Task CreateTasksForNewPhaseAsync(
        WorkPackage wp, WorkPackagePhase phase, List<CreateTaskRequest>? tasks,
        string changedBy, CancellationToken ct)
    {
        if (tasks is not { Count: > 0 }) return;

        var nextSortOrder = (await db.WorkPackageTasks
            .Where(t => t.WorkPackageId == wp.Id)
            .MaxAsync(t => (int?)t.SortOrder, ct) ?? 0) + 1;

        foreach (var taskReq in tasks)
        {
            var taskSortOrder = taskReq.SortOrder ?? nextSortOrder++;

            var task = new WorkPackageTask
            {
                TaskNumber = wp.NextTaskNumber++,
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

            StateTransitionHelper.ApplyStateTimestamps(task, CompletionState.NotStarted, taskReq.State);
            db.WorkPackageTasks.Add(task);
            AuditTaskCreation(task, changedBy);
        }
    }

    private void AuditTaskCreation(WorkPackageTask task, string changedBy)
    {
        var taskAudit = new List<TaskAuditLog>();
        var taskCreateAudit = () => new TaskAuditLog { Task = task, FieldName = default!, ChangedBy = changedBy, ChangedAt = DateTimeOffset.UtcNow };
        AuditHelper.AddCreateEntry(taskAudit, taskCreateAudit, "Name", task.Name);
        AuditHelper.AddCreateEntry(taskAudit, taskCreateAudit, "Description", task.Description);
        AuditHelper.AddCreateEntry(taskAudit, taskCreateAudit, "SortOrder", task.SortOrder.ToString());
        AuditHelper.AddCreateEntry(taskAudit, taskCreateAudit, "ImplementationNotes", task.ImplementationNotes);
        AuditHelper.AddCreateEntry(taskAudit, taskCreateAudit, "State", task.State.ToString());
        if (task.TargetFiles.Count > 0)
            AuditHelper.AddCreateEntry(taskAudit, taskCreateAudit, "TargetFiles", JsonSerializer.Serialize(task.TargetFiles.Select(f => new { f.FileName, f.RelativePath, f.Description })));
        if (task.Attachments.Count > 0)
            AuditHelper.AddCreateEntry(taskAudit, taskCreateAudit, "Attachments", JsonSerializer.Serialize(task.Attachments.Select(f => new { f.FileName, f.RelativePath, f.Description })));
        db.TaskAuditLogs.AddRange(taskAudit);
    }

    private void AuditPhaseCreation(WorkPackagePhase phase, string changedBy)
    {
        var phaseAudit = new List<PhaseAuditLog>();
        var phaseCreateAudit = () => new PhaseAuditLog { Phase = phase, FieldName = default!, ChangedBy = changedBy, ChangedAt = DateTimeOffset.UtcNow };
        AuditHelper.AddCreateEntry(phaseAudit, phaseCreateAudit, "Name", phase.Name);
        AuditHelper.AddCreateEntry(phaseAudit, phaseCreateAudit, "Description", phase.Description);
        AuditHelper.AddCreateEntry(phaseAudit, phaseCreateAudit, "SortOrder", phase.SortOrder.ToString());
        AuditHelper.AddCreateEntry(phaseAudit, phaseCreateAudit, "State", phase.State.ToString());
        db.PhaseAuditLogs.AddRange(phaseAudit);
    }

    // ── UpdateAsync helpers ──

    private static void AuditAndUpdatePhaseFields(
        WorkPackagePhase phase, UpdatePhaseRequest request,
        List<PhaseAuditLog> auditEntries, Func<PhaseAuditLog> audit)
    {
        if (request.Name is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "Name", phase.Name, request.Name, v => phase.Name = v);

        if (request.Description is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "Description", phase.Description, request.Description, v => phase.Description = v);

        if (request.SortOrder is not null)
            AuditHelper.AuditAndSetValue(auditEntries, audit, "SortOrder", phase.SortOrder, request.SortOrder.Value, v => phase.SortOrder = v);

        if (request.State is not null)
            AuditHelper.AuditAndSetEnum(auditEntries, audit, "State", phase.State, request.State.Value, v => phase.State = v);
    }

    private async Task HandlePhaseStateCascadesAsync(
        WorkPackagePhase phase, WorkPackage wp, UpdatePhaseRequest request,
        CompletionState oldPhaseState, string changedBy, List<StateChangeDto> stateChanges, CancellationToken ct)
    {
        if (request.State == CompletionState.Cancelled && oldPhaseState != CompletionState.Cancelled)
            await cascadeService.CascadePhaseCancellationAsync(phase, wp, changedBy, stateChanges, ct);

        if (request.State is not null && oldPhaseState != request.State.Value)
            cascadeService.AutoActivateWpFromPhase(phase, wp, changedBy, stateChanges);
    }

    private async Task<(List<WorkPackageTask> tasksReachingTerminal, HashSet<long> affectedPhaseIds)>
        UpsertTasksAsync(
            WorkPackagePhase phase, WorkPackage wp, List<UpsertTaskInPhaseDto>? tasks,
            string changedBy, DateTimeOffset now, List<TaskAuditLog> taskAuditEntries, CancellationToken ct)
    {
        var tasksReachingTerminal = new List<WorkPackageTask>();
        var affectedPhaseIds = new HashSet<long> { phase.Id };

        if (tasks is not { Count: > 0 })
            return (tasksReachingTerminal, affectedPhaseIds);

        var nextNewSortOrder = (await db.WorkPackageTasks
            .Where(t => t.WorkPackageId == wp.Id)
            .MaxAsync(t => (int?)t.SortOrder, ct) ?? 0) + 1;

        foreach (var taskDto in tasks)
        {
            if (taskDto.TaskNumber is not null)
                await UpdateExistingTaskAsync(wp, taskDto, changedBy, now, taskAuditEntries, affectedPhaseIds, tasksReachingTerminal, ct);
            else
                CreateNewTaskInPhase(wp, phase, taskDto, ref nextNewSortOrder, changedBy);
        }

        return (tasksReachingTerminal, affectedPhaseIds);
    }

    private async Task UpdateExistingTaskAsync(
        WorkPackage wp, UpsertTaskInPhaseDto taskDto, string changedBy, DateTimeOffset now,
        List<TaskAuditLog> taskAuditEntries, HashSet<long> affectedPhaseIds,
        List<WorkPackageTask> tasksReachingTerminal, CancellationToken ct)
    {
        var existingTask = await db.WorkPackageTasks
            .FirstOrDefaultAsync(t => t.WorkPackageId == wp.Id && t.TaskNumber == taskDto.TaskNumber!.Value, ct);

        if (existingTask is null)
            return;

        var oldTaskState = existingTask.State;
        var taskAudit = () => new TaskAuditLog { TaskId = existingTask.Id, FieldName = default!, ChangedBy = changedBy, ChangedAt = now };

        if (taskDto.Name is not null)
            AuditHelper.AuditAndSet(taskAuditEntries, taskAudit, "Name", existingTask.Name, taskDto.Name, v => existingTask.Name = v);

        if (taskDto.Description is not null)
            AuditHelper.AuditAndSet(taskAuditEntries, taskAudit, "Description", existingTask.Description, taskDto.Description, v => existingTask.Description = v);

        if (taskDto.SortOrder is not null)
            AuditHelper.AuditAndSetValue(taskAuditEntries, taskAudit, "SortOrder", existingTask.SortOrder, taskDto.SortOrder.Value, v => existingTask.SortOrder = v);

        if (taskDto.ImplementationNotes is not null)
            AuditHelper.AuditAndSet(taskAuditEntries, taskAudit, "ImplementationNotes", existingTask.ImplementationNotes, taskDto.ImplementationNotes, v => existingTask.ImplementationNotes = v);

        if (taskDto.State is not null)
            AuditHelper.AuditAndSetEnum(taskAuditEntries, taskAudit, "State", existingTask.State, taskDto.State.Value, v => existingTask.State = v);

        AuditFileReferences(existingTask, taskDto, taskAuditEntries, changedBy, now);

        if (taskDto.State is not null && oldTaskState != taskDto.State.Value)
        {
            StateTransitionHelper.ApplyBlockedStateLogic(existingTask, oldTaskState, taskDto.State.Value);
            StateTransitionHelper.ApplyStateTimestamps(existingTask, oldTaskState, taskDto.State.Value);

            affectedPhaseIds.Add(existingTask.PhaseId);
            if (CompletionStateConstants.TerminalStates.Contains(taskDto.State.Value))
                tasksReachingTerminal.Add(existingTask);
        }
    }

    private static void AuditFileReferences(
        WorkPackageTask task, UpsertTaskInPhaseDto taskDto,
        List<TaskAuditLog> auditEntries, string changedBy, DateTimeOffset now)
    {
        if (taskDto.TargetFiles is not null)
        {
            var oldJson = JsonSerializer.Serialize(task.TargetFiles.Select(f => new { f.FileName, f.RelativePath, f.Description }));
            task.TargetFiles = StateTransitionHelper.MapFileReferences(taskDto.TargetFiles);
            var newJson = JsonSerializer.Serialize(task.TargetFiles.Select(f => new { f.FileName, f.RelativePath, f.Description }));
            if (oldJson != newJson)
                auditEntries.Add(new TaskAuditLog { TaskId = task.Id, FieldName = "TargetFiles", OldValue = oldJson, NewValue = newJson, ChangedBy = changedBy, ChangedAt = now });
        }

        if (taskDto.Attachments is not null)
        {
            var oldJson = JsonSerializer.Serialize(task.Attachments.Select(f => new { f.FileName, f.RelativePath, f.Description }));
            task.Attachments = StateTransitionHelper.MapFileReferences(taskDto.Attachments);
            var newJson = JsonSerializer.Serialize(task.Attachments.Select(f => new { f.FileName, f.RelativePath, f.Description }));
            if (oldJson != newJson)
                auditEntries.Add(new TaskAuditLog { TaskId = task.Id, FieldName = "Attachments", OldValue = oldJson, NewValue = newJson, ChangedBy = changedBy, ChangedAt = now });
        }
    }

    private void CreateNewTaskInPhase(
        WorkPackage wp, WorkPackagePhase phase, UpsertTaskInPhaseDto taskDto,
        ref int nextSortOrder, string changedBy)
    {
        var taskSortOrder = taskDto.SortOrder ?? nextSortOrder++;

        var newTask = new WorkPackageTask
        {
            TaskNumber = wp.NextTaskNumber++,
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

        StateTransitionHelper.ApplyStateTimestamps(newTask, CompletionState.NotStarted, newTask.State);
        StateTransitionHelper.ApplyBlockedStateLogic(newTask, CompletionState.NotStarted, newTask.State);

        db.WorkPackageTasks.Add(newTask);
        AuditTaskCreation(newTask, changedBy);
    }

    private async Task ProcessTaskStateCascadesAsync(
        List<WorkPackageTask> tasksReachingTerminal, HashSet<long> affectedPhaseIds,
        long currentPhaseId, WorkPackage wp, string changedBy,
        List<StateChangeDto> stateChanges, CancellationToken ct)
    {
        foreach (var terminalTask in tasksReachingTerminal)
            await cascadeService.AutoUnblockDependentTasksAsync(terminalTask, wp, stateChanges, ct);

        foreach (var affectedPhaseId in affectedPhaseIds)
            await cascadeService.PropagateStateUpwardAsync(affectedPhaseId, wp, changedBy, stateChanges, ct);
    }

    // ── Response mapping ──

    private static PhaseResponse ToResponse(WorkPackagePhase p) =>
        ResponseMapper.MapPhase(p, p.WorkPackage.ProjectId, p.WorkPackage.WorkPackageNumber);
}
