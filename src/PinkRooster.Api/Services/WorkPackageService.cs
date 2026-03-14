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

            var project = await db.Projects.FirstAsync(p => p.Id == projectId, cancellation);
            var nextNumber = project.NextWpNumber;
            project.NextWpNumber++;

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
