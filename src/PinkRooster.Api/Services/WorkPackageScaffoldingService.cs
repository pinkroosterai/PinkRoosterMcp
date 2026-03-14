using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class WorkPackageScaffoldingService(AppDbContext db, IStateCascadeService cascadeService, IEventBroadcaster broadcaster) : IWorkPackageScaffoldingService
{
    public async Task<ScaffoldWorkPackageResponse> ScaffoldAsync(
        long projectId, ScaffoldWorkPackageRequest request, string changedBy,
        List<StateChangeDto>? stateChanges = null, CancellationToken ct = default)
    {
        stateChanges ??= [];

        // 0. Validate task dependency graphs upfront (before touching the change tracker)
        //    This prevents orphaned entities if RequestLoggingMiddleware calls SaveChangesAsync
        //    on the same scoped DbContext after we return a 400.
        foreach (var phaseReq in request.Phases)
        {
            if (phaseReq.Tasks is not { Count: > 0 }) continue;

            for (var ti = 0; ti < phaseReq.Tasks.Count; ti++)
            {
                var deps = phaseReq.Tasks[ti].DependsOnTaskIndices;
                if (deps is null) continue;

                foreach (var depIndex in deps)
                {
                    if (depIndex < 0 || depIndex >= phaseReq.Tasks.Count)
                        throw new InvalidOperationException(
                            $"Task '{phaseReq.Tasks[ti].Name}' in phase '{phaseReq.Name}' has out-of-bounds dependency index {depIndex}. " +
                            $"Valid range: 0-{phaseReq.Tasks.Count - 1}.");

                    if (depIndex == ti)
                        throw new InvalidOperationException(
                            $"Task '{phaseReq.Tasks[ti].Name}' in phase '{phaseReq.Name}' cannot depend on itself (index {depIndex}).");
                }
            }

            ValidateNoCycles(phaseReq, phaseReq.Tasks.Count);
        }

        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async (cancellation) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellation);

            // 1. Create Work Package
            var project = await db.Projects.FirstAsync(p => p.Id == projectId, cancellation);
            var nextWpNumber = project.NextWpNumber;
            project.NextWpNumber++;

            var wp = new WorkPackage
            {
                WorkPackageNumber = nextWpNumber,
                ProjectId = projectId,
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                Priority = request.Priority,
                Plan = request.Plan,
                EstimatedComplexity = request.EstimatedComplexity,
                EstimationRationale = request.EstimationRationale,
                State = request.State,
                Attachments = StateTransitionHelper.MapFileReferences(request.Attachments)
            };

            StateTransitionHelper.ApplyBlockedStateLogic(wp, CompletionState.NotStarted, request.State);
            StateTransitionHelper.ApplyStateTimestamps(wp, CompletionState.NotStarted, request.State);

            db.WorkPackages.Add(wp);
            {
                var wpAuditEntries = new List<WorkPackageAuditLog>();
                var wpCreateAudit = () => new WorkPackageAuditLog { WorkPackage = wp, FieldName = default!, ChangedBy = changedBy, ChangedAt = DateTimeOffset.UtcNow };
                AuditHelper.AddCreateEntry(wpAuditEntries, wpCreateAudit, "Name", wp.Name);
                AuditHelper.AddCreateEntry(wpAuditEntries, wpCreateAudit, "Description", wp.Description);
                AuditHelper.AddCreateEntry(wpAuditEntries, wpCreateAudit, "Type", wp.Type.ToString());
                AuditHelper.AddCreateEntry(wpAuditEntries, wpCreateAudit, "Priority", wp.Priority.ToString());
                AuditHelper.AddCreateEntry(wpAuditEntries, wpCreateAudit, "Plan", wp.Plan);
                AuditHelper.AddCreateEntry(wpAuditEntries, wpCreateAudit, "EstimatedComplexity", wp.EstimatedComplexity?.ToString());
                AuditHelper.AddCreateEntry(wpAuditEntries, wpCreateAudit, "EstimationRationale", wp.EstimationRationale);
                AuditHelper.AddCreateEntry(wpAuditEntries, wpCreateAudit, "State", wp.State.ToString());
                if (wp.Attachments.Count > 0)
                    AuditHelper.AddCreateEntry(wpAuditEntries, wpCreateAudit, "Attachments", JsonSerializer.Serialize(wp.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description })));

                // Create entity links (many-to-many)
                await CreateEntityLinksAsync(wp, request.LinkedIssueIds, request.LinkedFeatureRequestIds, wpAuditEntries, wpCreateAudit, cancellation);

                db.WorkPackageAuditLogs.AddRange(wpAuditEntries);
            }

            // 2. Create Phases, Tasks, AcceptanceCriteria
            var nextPhaseNumber = 1;
            var nextPhaseSortOrder = 1;
            var nextTaskNumber = 1;
            var totalDependencies = 0;

            // Track created tasks per phase for dependency resolution: phaseIndex → list of task entities
            var phaseTaskMap = new List<List<WorkPackageTask>>();
            var phaseEntities = new List<WorkPackagePhase>();

            foreach (var phaseReq in request.Phases)
            {
                var phaseSortOrder = phaseReq.SortOrder ?? nextPhaseSortOrder++;
                if (phaseReq.SortOrder is not null)
                    nextPhaseSortOrder = Math.Max(nextPhaseSortOrder, phaseSortOrder + 1);

                var phase = new WorkPackagePhase
                {
                    PhaseNumber = nextPhaseNumber++,
                    WorkPackage = wp,
                    Name = phaseReq.Name,
                    Description = phaseReq.Description,
                    SortOrder = phaseSortOrder,
                    State = CompletionState.NotStarted
                };

                db.WorkPackagePhases.Add(phase);
                {
                    var phaseAuditEntries = new List<PhaseAuditLog>();
                    var phaseCreateAudit = () => new PhaseAuditLog { Phase = phase, FieldName = default!, ChangedBy = changedBy, ChangedAt = DateTimeOffset.UtcNow };
                    AuditHelper.AddCreateEntry(phaseAuditEntries, phaseCreateAudit, "Name", phase.Name);
                    AuditHelper.AddCreateEntry(phaseAuditEntries, phaseCreateAudit, "Description", phase.Description);
                    AuditHelper.AddCreateEntry(phaseAuditEntries, phaseCreateAudit, "SortOrder", phase.SortOrder.ToString());
                    AuditHelper.AddCreateEntry(phaseAuditEntries, phaseCreateAudit, "State", phase.State.ToString());
                    db.PhaseAuditLogs.AddRange(phaseAuditEntries);
                }
                phaseEntities.Add(phase);

                // Acceptance Criteria
                if (phaseReq.AcceptanceCriteria is { Count: > 0 })
                {
                    foreach (var acDto in phaseReq.AcceptanceCriteria)
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

                // Tasks
                var phaseTasks = new List<WorkPackageTask>();
                var nextTaskSortOrder = 1;

                if (phaseReq.Tasks is { Count: > 0 })
                {
                    foreach (var taskReq in phaseReq.Tasks)
                    {
                        var taskSortOrder = taskReq.SortOrder ?? nextTaskSortOrder++;
                        if (taskReq.SortOrder is not null)
                            nextTaskSortOrder = Math.Max(nextTaskSortOrder, taskSortOrder + 1);

                        var task = new WorkPackageTask
                        {
                            TaskNumber = nextTaskNumber++,
                            Phase = phase,
                            WorkPackage = wp,
                            Name = taskReq.Name,
                            Description = taskReq.Description,
                            SortOrder = taskSortOrder,
                            ImplementationNotes = taskReq.ImplementationNotes,
                            State = taskReq.State,
                            TargetFiles = StateTransitionHelper.MapFileReferences(taskReq.TargetFiles),
                            Attachments = StateTransitionHelper.MapFileReferences(taskReq.Attachments)
                        };

                        StateTransitionHelper.ApplyStateTimestamps(task, CompletionState.NotStarted, taskReq.State);
                        StateTransitionHelper.ApplyBlockedStateLogic(task, CompletionState.NotStarted, taskReq.State);

                        db.WorkPackageTasks.Add(task);
                        {
                            var taskAuditEntries = new List<TaskAuditLog>();
                            var taskCreateAudit = () => new TaskAuditLog { Task = task, FieldName = default!, ChangedBy = changedBy, ChangedAt = DateTimeOffset.UtcNow };
                            AuditHelper.AddCreateEntry(taskAuditEntries, taskCreateAudit, "Name", task.Name);
                            AuditHelper.AddCreateEntry(taskAuditEntries, taskCreateAudit, "Description", task.Description);
                            AuditHelper.AddCreateEntry(taskAuditEntries, taskCreateAudit, "SortOrder", task.SortOrder.ToString());
                            AuditHelper.AddCreateEntry(taskAuditEntries, taskCreateAudit, "ImplementationNotes", task.ImplementationNotes);
                            AuditHelper.AddCreateEntry(taskAuditEntries, taskCreateAudit, "State", task.State.ToString());
                            if (task.TargetFiles.Count > 0)
                                AuditHelper.AddCreateEntry(taskAuditEntries, taskCreateAudit, "TargetFiles", JsonSerializer.Serialize(task.TargetFiles.Select(f => new { f.FileName, f.RelativePath, f.Description })));
                            if (task.Attachments.Count > 0)
                                AuditHelper.AddCreateEntry(taskAuditEntries, taskCreateAudit, "Attachments", JsonSerializer.Serialize(task.Attachments.Select(f => new { f.FileName, f.RelativePath, f.Description })));
                            db.TaskAuditLogs.AddRange(taskAuditEntries);
                        }
                        phaseTasks.Add(task);
                    }
                }

                phaseTaskMap.Add(phaseTasks);
            }

            // 3. Create Task Dependencies (same-phase only, already validated upfront)
            for (var pi = 0; pi < request.Phases.Count; pi++)
            {
                var phaseReq = request.Phases[pi];
                if (phaseReq.Tasks is null) continue;

                for (var ti = 0; ti < phaseReq.Tasks.Count; ti++)
                {
                    var taskReq = phaseReq.Tasks[ti];
                    if (taskReq.DependsOnTaskIndices is not { Count: > 0 }) continue;

                    var dependentTask = phaseTaskMap[pi][ti];

                    foreach (var depIndex in taskReq.DependsOnTaskIndices)
                    {
                        var dependsOnTask = phaseTaskMap[pi][depIndex];

                        db.WorkPackageTaskDependencies.Add(new WorkPackageTaskDependency
                        {
                            DependentTask = dependentTask,
                            DependsOnTask = dependsOnTask,
                            Reason = $"Scaffold dependency: {dependsOnTask.Name} → {dependentTask.Name}"
                        });

                        cascadeService.AutoBlockTaskIfNeeded(dependentTask, dependsOnTask, wp, stateChanges);

                        totalDependencies++;
                    }
                }
            }

            // Update sequential number counters on the WP
            wp.NextPhaseNumber = nextPhaseNumber;
            wp.NextTaskNumber = nextTaskNumber;

            // 4. WP-level Dependencies (blockedBy existing WPs)
            if (request.BlockedByWpIds is { Count: > 0 })
            {
                // SaveChanges first so WP gets an Id for dependency FK
                await db.SaveChangesAsync(cancellation);

                foreach (var blockerWpId in request.BlockedByWpIds)
                {
                    var blockerWp = await db.WorkPackages
                        .FirstOrDefaultAsync(w => w.Id == blockerWpId, cancellation)
                        ?? throw new InvalidOperationException(
                            $"Blocker work package with internal ID {blockerWpId} not found.");

                    if (blockerWp.Id == wp.Id)
                        throw new InvalidOperationException("A work package cannot depend on itself.");

                    if (await cascadeService.HasCircularWpDependencyAsync(wp.Id, blockerWp.Id, cancellation))
                        throw new InvalidOperationException(
                            $"Adding dependency on 'proj-{blockerWp.ProjectId}-wp-{blockerWp.WorkPackageNumber}' would create a circular dependency.");

                    db.WorkPackageDependencies.Add(new WorkPackageDependency
                    {
                        DependentWorkPackageId = wp.Id,
                        DependsOnWorkPackageId = blockerWp.Id,
                        Reason = "Scaffold dependency"
                    });

                    cascadeService.AutoBlockWpIfNeeded(wp, blockerWp, stateChanges);

                    totalDependencies++;
                }
            }

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);

            broadcaster.Publish(new ServerEvent
            {
                EventType = "entity:changed",
                EntityType = "WorkPackage",
                EntityId = $"proj-{projectId}-wp-{nextWpNumber}",
                Action = "created",
                ProjectId = projectId,
                StateChanges = stateChanges.Count > 0 ? stateChanges : null
            });

            // 5. Build compact response
            var totalTasks = phaseTaskMap.Sum(pt => pt.Count);
            var phaseResults = new List<ScaffoldPhaseResult>();

            for (var i = 0; i < phaseEntities.Count; i++)
            {
                var phase = phaseEntities[i];
                phaseResults.Add(new ScaffoldPhaseResult
                {
                    PhaseId = $"proj-{projectId}-wp-{nextWpNumber}-phase-{phase.PhaseNumber}",
                    TaskIds = phaseTaskMap[i].Select(t =>
                        $"proj-{projectId}-wp-{nextWpNumber}-task-{t.TaskNumber}").ToList()
                });
            }

            return new ScaffoldWorkPackageResponse
            {
                WorkPackageId = $"proj-{projectId}-wp-{nextWpNumber}",
                Phases = phaseResults,
                TotalTasks = totalTasks,
                TotalDependencies = totalDependencies,
                StateChanges = stateChanges.Count > 0 ? stateChanges : null
            };
        }, ct);
    }

    private static void ValidateNoCycles(ScaffoldPhaseRequest phaseReq, int taskCount)
    {
        if (phaseReq.Tasks is null) return;

        // Build adjacency: depIndex → taskIndex (dependsOn → dependent)
        var inDegree = new int[taskCount];
        var adj = new List<int>[taskCount];
        for (var i = 0; i < taskCount; i++) adj[i] = [];

        for (var ti = 0; ti < phaseReq.Tasks.Count; ti++)
        {
            var deps = phaseReq.Tasks[ti].DependsOnTaskIndices;
            if (deps is null) continue;

            foreach (var depIdx in deps)
            {
                if (depIdx < 0 || depIdx >= taskCount) continue; // already validated above
                adj[depIdx].Add(ti);
                inDegree[ti]++;
            }
        }

        // Kahn's algorithm
        var queue = new Queue<int>();
        for (var i = 0; i < taskCount; i++)
            if (inDegree[i] == 0) queue.Enqueue(i);

        var visited = 0;
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            visited++;
            foreach (var next in adj[node])
            {
                inDegree[next]--;
                if (inDegree[next] == 0) queue.Enqueue(next);
            }
        }

        if (visited < taskCount)
            throw new InvalidOperationException(
                $"Circular task dependency detected in phase '{phaseReq.Name}'. " +
                $"Review the dependsOnTaskIndices to remove the cycle.");
    }

    private async Task CreateEntityLinksAsync(
        WorkPackage wp, List<long>? issueIds, List<long>? frIds,
        List<WorkPackageAuditLog> auditEntries, Func<WorkPackageAuditLog> createAudit, CancellationToken ct)
    {
        if (issueIds is { Count: > 0 })
        {
            var issues = await db.Issues.Where(i => issueIds.Contains(i.Id)).ToListAsync(ct);
            foreach (var issue in issues)
                wp.LinkedIssueLinks.Add(new WorkPackageIssueLink { WorkPackage = wp, Issue = issue });

            AuditHelper.AddCreateEntry(auditEntries, createAudit, "LinkedIssueIds",
                string.Join(",", issues.Select(i => i.Id)));
        }

        if (frIds is { Count: > 0 })
        {
            var frs = await db.FeatureRequests.Where(fr => frIds.Contains(fr.Id)).ToListAsync(ct);
            foreach (var fr in frs)
                wp.LinkedFeatureRequestLinks.Add(new WorkPackageFeatureRequestLink { WorkPackage = wp, FeatureRequest = fr });

            AuditHelper.AddCreateEntry(auditEntries, createAudit, "LinkedFeatureRequestIds",
                string.Join(",", frs.Select(fr => fr.Id)));
        }
    }
}
