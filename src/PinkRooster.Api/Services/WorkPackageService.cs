using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class WorkPackageService(AppDbContext db, IStateCascadeService cascadeService, IEventBroadcaster broadcaster) : IWorkPackageService
{
    public async Task<List<WorkPackageResponse>> GetByProjectAsync(
        long projectId, string? stateFilter, CancellationToken ct = default)
    {
        var query = db.WorkPackages.Where(w => w.ProjectId == projectId);
        query = ApplyStateFilter(query, stateFilter);

        var wps = await query
            .Include(w => w.LinkedIssueLinks).ThenInclude(l => l.Issue)
            .Include(w => w.LinkedFeatureRequestLinks).ThenInclude(l => l.FeatureRequest)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

        return wps.Select(ToListResponse).ToList();
    }

    public async Task<WorkPackageResponse?> GetByNumberAsync(
        long projectId, int wpNumber, CancellationToken ct = default)
    {
        var wp = await db.WorkPackages
            .Include(w => w.Phases.OrderBy(p => p.SortOrder))
                .ThenInclude(p => p.Tasks.OrderBy(t => t.SortOrder))
                    .ThenInclude(t => t.BlockedBy).ThenInclude(d => d.DependsOnTask)
            .Include(w => w.Phases)
                .ThenInclude(p => p.Tasks)
                    .ThenInclude(t => t.Blocking).ThenInclude(d => d.DependentTask)
            .Include(w => w.Phases)
                .ThenInclude(p => p.AcceptanceCriteria)
            .Include(w => w.BlockedBy).ThenInclude(d => d.DependsOnWorkPackage)
            .Include(w => w.Blocking).ThenInclude(d => d.DependentWorkPackage)
            .Include(w => w.LinkedIssueLinks).ThenInclude(l => l.Issue)
            .Include(w => w.LinkedFeatureRequestLinks).ThenInclude(l => l.FeatureRequest)
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct);

        return wp is null ? null : ToResponse(wp);
    }

    public async Task<WorkPackageSummaryResponse> GetSummaryAsync(long projectId, CancellationToken ct = default)
    {
        var query = db.WorkPackages.Where(w => w.ProjectId == projectId);

        return new WorkPackageSummaryResponse
        {
            ActiveCount = await query.CountAsync(w => CompletionStateConstants.ActiveStates.Contains(w.State), ct),
            InactiveCount = await query.CountAsync(w => CompletionStateConstants.InactiveStates.Contains(w.State), ct),
            TerminalCount = await query.CountAsync(w => CompletionStateConstants.TerminalStates.Contains(w.State), ct)
        };
    }

    public async Task<WorkPackageResponse> CreateAsync(
        long projectId, CreateWorkPackageRequest request, string changedBy, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async (cancellation) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellation);

            var nextNumber = await db.WorkPackages
                .Where(w => w.ProjectId == projectId)
                .MaxAsync(w => (int?)w.WorkPackageNumber, cancellation) ?? 0;
            nextNumber++;

            var wp = new WorkPackage
            {
                WorkPackageNumber = nextNumber,
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

            // Apply blocked state logic (fixes: previously dead code that never set PreviousActiveState)
            StateTransitionHelper.ApplyBlockedStateLogic(wp, CompletionState.NotStarted, request.State);

            // Apply state-driven timestamps
            StateTransitionHelper.ApplyStateTimestamps(wp, CompletionState.NotStarted, request.State);

            db.WorkPackages.Add(wp);

            // Audit all fields on creation
            var auditEntries = new List<WorkPackageAuditLog>();
            var createAudit = () => new WorkPackageAuditLog { WorkPackage = wp, FieldName = default!, ChangedBy = changedBy, ChangedAt = DateTimeOffset.UtcNow };
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Name", wp.Name);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Description", wp.Description);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Type", wp.Type.ToString());
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Priority", wp.Priority.ToString());
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Plan", wp.Plan);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "EstimatedComplexity", wp.EstimatedComplexity?.ToString());
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "EstimationRationale", wp.EstimationRationale);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "State", wp.State.ToString());
            if (wp.Attachments.Count > 0)
                AuditHelper.AddCreateEntry(auditEntries, createAudit, "Attachments", JsonSerializer.Serialize(wp.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description })));

            // Create entity links (many-to-many)
            await CreateEntityLinksAsync(wp, request.LinkedIssueIds, request.LinkedFeatureRequestIds, auditEntries, createAudit, cancellation);

            db.WorkPackageAuditLogs.AddRange(auditEntries);

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);

            broadcaster.Publish(new ServerEvent
            {
                EventType = "entity:changed",
                EntityType = "WorkPackage",
                EntityId = $"proj-{projectId}-wp-{wp.WorkPackageNumber}",
                Action = "created",
                ProjectId = projectId
            });

            return ToListResponse(wp);
        }, ct);
    }

    public async Task<WorkPackageResponse?> UpdateAsync(
        long projectId, int wpNumber, UpdateWorkPackageRequest request, string changedBy, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default)
    {
        stateChanges ??= [];

        var wp = await db.WorkPackages
            .Include(w => w.Phases.OrderBy(p => p.SortOrder))
                .ThenInclude(p => p.Tasks.OrderBy(t => t.SortOrder))
                    .ThenInclude(t => t.BlockedBy).ThenInclude(d => d.DependsOnTask)
            .Include(w => w.Phases)
                .ThenInclude(p => p.Tasks)
                    .ThenInclude(t => t.Blocking).ThenInclude(d => d.DependentTask)
            .Include(w => w.Phases)
                .ThenInclude(p => p.AcceptanceCriteria)
            .Include(w => w.BlockedBy).ThenInclude(d => d.DependsOnWorkPackage)
            .Include(w => w.Blocking).ThenInclude(d => d.DependentWorkPackage)
            .Include(w => w.LinkedIssueLinks).ThenInclude(l => l.Issue)
            .Include(w => w.LinkedFeatureRequestLinks).ThenInclude(l => l.FeatureRequest)
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct);

        if (wp is null)
            return null;

        var auditEntries = new List<WorkPackageAuditLog>();
        var now = DateTimeOffset.UtcNow;
        var audit = () => new WorkPackageAuditLog { WorkPackageId = wp.Id, FieldName = default!, ChangedBy = changedBy, ChangedAt = now };

        // Track state before changes for timestamp logic
        var oldState = wp.State;

        if (request.Name is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "Name", wp.Name, request.Name, v => wp.Name = v);

        if (request.Description is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "Description", wp.Description, request.Description, v => wp.Description = v);

        if (request.Type is not null)
            AuditHelper.AuditAndSetEnum(auditEntries, audit, "Type", wp.Type, request.Type.Value, v => wp.Type = v);

        if (request.Priority is not null)
            AuditHelper.AuditAndSetEnum(auditEntries, audit, "Priority", wp.Priority, request.Priority.Value, v => wp.Priority = v);

        if (request.Plan is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "Plan", wp.Plan, request.Plan, v => wp.Plan = v);

        if (request.EstimatedComplexity is not null)
            AuditHelper.AuditAndSetNullable(auditEntries, audit, "EstimatedComplexity", wp.EstimatedComplexity, request.EstimatedComplexity, v => wp.EstimatedComplexity = v);

        if (request.EstimationRationale is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "EstimationRationale", wp.EstimationRationale, request.EstimationRationale, v => wp.EstimationRationale = v);

        if (request.State is not null)
            AuditHelper.AuditAndSetEnum(auditEntries, audit, "State", wp.State, request.State.Value, v => wp.State = v);

        // Set-based link replacement: null = don't change, empty = clear all, non-empty = replace all
        if (request.LinkedIssueIds is not null)
        {
            var oldIds = wp.LinkedIssueLinks.Select(l => l.IssueId).OrderBy(x => x).ToList();
            var newIds = request.LinkedIssueIds.Distinct().OrderBy(x => x).ToList();

            if (!oldIds.SequenceEqual(newIds))
            {
                auditEntries.Add(new WorkPackageAuditLog
                {
                    WorkPackageId = wp.Id, FieldName = "LinkedIssueIds",
                    OldValue = oldIds.Count > 0 ? string.Join(",", oldIds) : null,
                    NewValue = newIds.Count > 0 ? string.Join(",", newIds) : null,
                    ChangedBy = changedBy, ChangedAt = now
                });

                db.WorkPackageIssueLinks.RemoveRange(wp.LinkedIssueLinks);
                wp.LinkedIssueLinks.Clear();

                if (newIds.Count > 0)
                {
                    var issues = await db.Issues.Where(i => newIds.Contains(i.Id)).ToListAsync(ct);
                    foreach (var issue in issues)
                        wp.LinkedIssueLinks.Add(new WorkPackageIssueLink { WorkPackageId = wp.Id, Issue = issue });
                }
            }
        }

        if (request.LinkedFeatureRequestIds is not null)
        {
            var oldIds = wp.LinkedFeatureRequestLinks.Select(l => l.FeatureRequestId).OrderBy(x => x).ToList();
            var newIds = request.LinkedFeatureRequestIds.Distinct().OrderBy(x => x).ToList();

            if (!oldIds.SequenceEqual(newIds))
            {
                auditEntries.Add(new WorkPackageAuditLog
                {
                    WorkPackageId = wp.Id, FieldName = "LinkedFeatureRequestIds",
                    OldValue = oldIds.Count > 0 ? string.Join(",", oldIds) : null,
                    NewValue = newIds.Count > 0 ? string.Join(",", newIds) : null,
                    ChangedBy = changedBy, ChangedAt = now
                });

                db.WorkPackageFeatureRequestLinks.RemoveRange(wp.LinkedFeatureRequestLinks);
                wp.LinkedFeatureRequestLinks.Clear();

                if (newIds.Count > 0)
                {
                    var frs = await db.FeatureRequests.Where(fr => newIds.Contains(fr.Id)).ToListAsync(ct);
                    foreach (var fr in frs)
                        wp.LinkedFeatureRequestLinks.Add(new WorkPackageFeatureRequestLink { WorkPackageId = wp.Id, FeatureRequest = fr });
                }
            }
        }

        if (request.Attachments is not null)
        {
            var oldJson = JsonSerializer.Serialize(wp.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description }));
            wp.Attachments = StateTransitionHelper.MapFileReferences(request.Attachments);
            var newJson = JsonSerializer.Serialize(wp.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description }));

            if (oldJson != newJson)
            {
                auditEntries.Add(new WorkPackageAuditLog
                {
                    WorkPackageId = wp.Id,
                    FieldName = "Attachments",
                    OldValue = oldJson,
                    NewValue = newJson,
                    ChangedBy = changedBy,
                    ChangedAt = now
                });
            }
        }

        // Apply blocked state logic and state-driven timestamps if state changed
        if (request.State is not null && oldState != request.State.Value)
        {
            var newState = request.State.Value;

            // Soft warning: moving to active state while non-terminal blockers exist
            if (CompletionStateConstants.ActiveStates.Contains(newState))
            {
                var activeBlockerCount = await db.WorkPackageDependencies
                    .Where(d => d.DependentWorkPackageId == wp.Id)
                    .Include(d => d.DependsOnWorkPackage)
                    .CountAsync(d => !CompletionStateConstants.TerminalStates.Contains(d.DependsOnWorkPackage.State), ct);

                if (activeBlockerCount > 0)
                {
                    stateChanges.Add(new StateChangeDto
                    {
                        EntityType = "WorkPackage",
                        EntityId = $"proj-{projectId}-wp-{wpNumber}",
                        OldState = oldState.ToString(),
                        NewState = newState.ToString(),
                        Reason = $"Warning: entity has {activeBlockerCount} non-terminal blocker(s) — dependency enforcement is advisory"
                    });
                }
            }

            StateTransitionHelper.ApplyBlockedStateLogic(wp, oldState, newState);
            StateTransitionHelper.ApplyStateTimestamps(wp, oldState, newState);
        }

        // Dep-completion auto-unblock: if WP transitioned to terminal, unblock dependents
        if (request.State is not null && oldState != request.State.Value
            && CompletionStateConstants.TerminalStates.Contains(request.State.Value))
        {
            await cascadeService.AutoUnblockDependentWpsAsync(wp, stateChanges, ct);
        }

        if (auditEntries.Count > 0)
            db.WorkPackageAuditLogs.AddRange(auditEntries);

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "WorkPackage",
            EntityId = $"proj-{projectId}-wp-{wpNumber}",
            Action = "updated",
            ProjectId = projectId,
            StateChanges = stateChanges.Count > 0 ? stateChanges : null
        });

        var response = ToResponse(wp);
        response.StateChanges = stateChanges.Count > 0 ? stateChanges : null;
        return response;
    }

    public async Task<bool> DeleteAsync(long projectId, int wpNumber, CancellationToken ct = default)
    {
        var wp = await db.WorkPackages
            .Include(w => w.Phases)
                .ThenInclude(p => p.Tasks)
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct);

        if (wp is null)
            return false;

        db.WorkPackages.Remove(wp);
        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "WorkPackage",
            EntityId = $"proj-{projectId}-wp-{wp.WorkPackageNumber}",
            Action = "deleted",
            ProjectId = projectId
        });

        return true;
    }

    public async Task<DependencyResponse> AddDependencyAsync(
        long projectId, int wpNumber, ManageDependencyRequest request, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default)
    {
        stateChanges ??= [];

        var dependentWp = await db.WorkPackages
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct)
            ?? throw new KeyNotFoundException($"Work package {wpNumber} not found in project {projectId}");

        var dependsOnWp = await db.WorkPackages
            .FirstOrDefaultAsync(w => w.Id == request.DependsOnId, ct)
            ?? throw new KeyNotFoundException($"Dependency target work package {request.DependsOnId} not found");

        // Validate: no self-dependency
        if (dependentWp.Id == dependsOnWp.Id)
            throw new InvalidOperationException("A work package cannot depend on itself.");

        // Validate: no duplicate
        var exists = await db.WorkPackageDependencies
            .AnyAsync(d => d.DependentWorkPackageId == dependentWp.Id && d.DependsOnWorkPackageId == dependsOnWp.Id, ct);
        if (exists)
            throw new InvalidOperationException("This dependency already exists.");

        // Validate: no circular dependency
        if (await cascadeService.HasCircularWpDependencyAsync(dependentWp.Id, dependsOnWp.Id, ct))
            throw new InvalidOperationException("Adding this dependency would create a circular dependency.");

        var dependency = new WorkPackageDependency
        {
            DependentWorkPackageId = dependentWp.Id,
            DependsOnWorkPackageId = dependsOnWp.Id,
            Reason = request.Reason
        };

        db.WorkPackageDependencies.Add(dependency);

        cascadeService.AutoBlockWpIfNeeded(dependentWp, dependsOnWp, stateChanges);

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "WorkPackage",
            EntityId = $"proj-{projectId}-wp-{wpNumber}",
            Action = "updated",
            ProjectId = projectId,
            StateChanges = stateChanges.Count > 0 ? stateChanges : null
        });

        var response = new DependencyResponse
        {
            WorkPackageId = $"proj-{dependsOnWp.ProjectId}-wp-{dependsOnWp.WorkPackageNumber}",
            Name = dependsOnWp.Name,
            State = dependsOnWp.State.ToString(),
            Reason = dependency.Reason,
            StateChanges = stateChanges.Count > 0 ? stateChanges : null
        };
        return response;
    }

    public async Task<bool> RemoveDependencyAsync(
        long projectId, int wpNumber, long dependsOnWpId, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default)
    {
        stateChanges ??= [];

        var dependentWp = await db.WorkPackages
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct);

        if (dependentWp is null)
            return false;

        var dependency = await db.WorkPackageDependencies
            .FirstOrDefaultAsync(d => d.DependentWorkPackageId == dependentWp.Id && d.DependsOnWorkPackageId == dependsOnWpId, ct);

        if (dependency is null)
            return false;

        db.WorkPackageDependencies.Remove(dependency);

        // After removal: if dependent WP is Blocked, check if remaining blockers exist
        if (dependentWp.State == CompletionState.Blocked)
        {
            var remainingBlockers = await db.WorkPackageDependencies
                .Where(d => d.DependentWorkPackageId == dependentWp.Id && d.DependsOnWorkPackageId != dependsOnWpId)
                .Include(d => d.DependsOnWorkPackage)
                .AnyAsync(d => !CompletionStateConstants.TerminalStates.Contains(d.DependsOnWorkPackage.State), ct);

            if (!remainingBlockers && dependentWp.PreviousActiveState is not null)
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
                    Reason = "Auto-unblocked: no remaining blockers"
                });
            }
        }

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "WorkPackage",
            EntityId = $"proj-{projectId}-wp-{dependentWp.WorkPackageNumber}",
            Action = "updated",
            ProjectId = projectId,
            StateChanges = stateChanges.Count > 0 ? stateChanges : null
        });

        return true;
    }

    public async Task<ScaffoldWorkPackageResponse> ScaffoldAsync(
        long projectId, ScaffoldWorkPackageRequest request, string changedBy, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default)
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
            var nextWpNumber = await db.WorkPackages
                .Where(w => w.ProjectId == projectId)
                .MaxAsync(w => (int?)w.WorkPackageNumber, cancellation) ?? 0;
            nextWpNumber++;

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

    // ── Private helpers ──

    private static IQueryable<WorkPackage> ApplyStateFilter(IQueryable<WorkPackage> query, string? stateFilter)
    {
        if (string.IsNullOrWhiteSpace(stateFilter))
            return query;

        var states = stateFilter.ToLowerInvariant() switch
        {
            "active" => CompletionStateConstants.ActiveStates,
            "inactive" => CompletionStateConstants.InactiveStates,
            "terminal" => CompletionStateConstants.TerminalStates,
            _ => null
        };

        return states is null ? query : query.Where(w => states.Contains(w.State));
    }

    private static WorkPackageResponse ToListResponse(WorkPackage w) => new()
    {
        WorkPackageId = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}",
        Id = w.Id,
        WorkPackageNumber = w.WorkPackageNumber,
        ProjectId = $"proj-{w.ProjectId}",
        Name = w.Name,
        Description = w.Description,
        Type = w.Type.ToString(),
        Priority = w.Priority.ToString(),
        Plan = w.Plan,
        EstimatedComplexity = w.EstimatedComplexity,
        EstimationRationale = w.EstimationRationale,
        State = w.State.ToString(),
        PreviousActiveState = w.PreviousActiveState?.ToString(),
        LinkedIssueIds = MapLinkedIssueIds(w),
        LinkedFeatureRequestIds = MapLinkedFrIds(w),
        StartedAt = w.StartedAt,
        CompletedAt = w.CompletedAt,
        ResolvedAt = w.ResolvedAt,
        Attachments = ResponseMapper.MapFileReferences(w.Attachments),
        CreatedAt = w.CreatedAt,
        UpdatedAt = w.UpdatedAt
    };

    private static WorkPackageResponse ToResponse(WorkPackage w) => new()
    {
        WorkPackageId = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}",
        Id = w.Id,
        WorkPackageNumber = w.WorkPackageNumber,
        ProjectId = $"proj-{w.ProjectId}",
        Name = w.Name,
        Description = w.Description,
        Type = w.Type.ToString(),
        Priority = w.Priority.ToString(),
        Plan = w.Plan,
        EstimatedComplexity = w.EstimatedComplexity,
        EstimationRationale = w.EstimationRationale,
        State = w.State.ToString(),
        PreviousActiveState = w.PreviousActiveState?.ToString(),
        LinkedIssueIds = MapLinkedIssueIds(w),
        LinkedFeatureRequestIds = MapLinkedFrIds(w),
        StartedAt = w.StartedAt,
        CompletedAt = w.CompletedAt,
        ResolvedAt = w.ResolvedAt,
        Attachments = ResponseMapper.MapFileReferences(w.Attachments),
        Phases = w.Phases.Select(p => ResponseMapper.MapPhase(p, w.ProjectId, w.WorkPackageNumber)).ToList(),
        BlockedBy = w.BlockedBy.Select(d => new DependencyResponse
        {
            WorkPackageId = $"proj-{d.DependsOnWorkPackage.ProjectId}-wp-{d.DependsOnWorkPackage.WorkPackageNumber}",
            Name = d.DependsOnWorkPackage.Name,
            State = d.DependsOnWorkPackage.State.ToString(),
            Reason = d.Reason
        }).ToList(),
        Blocking = w.Blocking.Select(d => new DependencyResponse
        {
            WorkPackageId = $"proj-{d.DependentWorkPackage.ProjectId}-wp-{d.DependentWorkPackage.WorkPackageNumber}",
            Name = d.DependentWorkPackage.Name,
            State = d.DependentWorkPackage.State.ToString(),
            Reason = d.Reason
        }).ToList(),
        CreatedAt = w.CreatedAt,
        UpdatedAt = w.UpdatedAt
    };

    private static List<string> MapLinkedIssueIds(WorkPackage w) =>
        w.LinkedIssueLinks.Select(l => $"proj-{w.ProjectId}-issue-{l.Issue.IssueNumber}").ToList();

    private static List<string> MapLinkedFrIds(WorkPackage w) =>
        w.LinkedFeatureRequestLinks.Select(l => $"proj-{w.ProjectId}-fr-{l.FeatureRequest.FeatureRequestNumber}").ToList();

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
