using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class WorkPackageService(AppDbContext db) : IWorkPackageService
{
    public async Task<List<WorkPackageResponse>> GetByProjectAsync(
        long projectId, string? stateFilter, CancellationToken ct = default)
    {
        var query = db.WorkPackages.Where(w => w.ProjectId == projectId);
        query = ApplyStateFilter(query, stateFilter);

        return await query
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => ToListResponse(w))
            .ToListAsync(ct);
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
            .Include(w => w.LinkedIssue)
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct);

        return wp is null ? null : ToResponse(wp);
    }

    public async Task<WorkPackageSummaryResponse> GetSummaryAsync(long projectId, CancellationToken ct = default)
    {
        var workPackages = await db.WorkPackages
            .Where(w => w.ProjectId == projectId)
            .ToListAsync(ct);

        return new WorkPackageSummaryResponse
        {
            ActiveCount = workPackages.Count(w => CompletionStateConstants.ActiveStates.Contains(w.State)),
            InactiveCount = workPackages.Count(w => CompletionStateConstants.InactiveStates.Contains(w.State)),
            TerminalCount = workPackages.Count(w => CompletionStateConstants.TerminalStates.Contains(w.State))
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
                LinkedIssueId = request.LinkedIssueId,
                Attachments = MapAttachments(request.Attachments)
            };

            // Apply blocked state logic
            if (request.State == CompletionState.Blocked &&
                CompletionStateConstants.ActiveStates.Contains(CompletionState.NotStarted))
            {
                // NotStarted is not active, so no PreviousActiveState to capture on create
            }

            // Apply state-driven timestamps
            ApplyStateTimestamps(wp, CompletionState.NotStarted, request.State);

            db.WorkPackages.Add(wp);

            // Audit all fields on creation
            var auditEntries = BuildCreateAuditEntries(wp, changedBy);
            db.WorkPackageAuditLogs.AddRange(auditEntries);

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);

            return ToListResponse(wp);
        }, ct);
    }

    public async Task<WorkPackageResponse?> UpdateAsync(
        long projectId, int wpNumber, UpdateWorkPackageRequest request, string changedBy, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default)
    {
        stateChanges ??= [];

        var wp = await db.WorkPackages
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct);

        if (wp is null)
            return null;

        var auditEntries = new List<WorkPackageAuditLog>();
        var now = DateTimeOffset.UtcNow;

        // Track state before changes for timestamp logic
        var oldState = wp.State;

        if (request.Name is not null)
            AuditAndSet(auditEntries, wp.Id, changedBy, now, "Name", wp.Name, request.Name, v => wp.Name = v);

        if (request.Description is not null)
            AuditAndSet(auditEntries, wp.Id, changedBy, now, "Description", wp.Description, request.Description, v => wp.Description = v);

        if (request.Type is not null)
            AuditAndSetEnum(auditEntries, wp.Id, changedBy, now, "Type", wp.Type, request.Type.Value, v => wp.Type = v);

        if (request.Priority is not null)
            AuditAndSetEnum(auditEntries, wp.Id, changedBy, now, "Priority", wp.Priority, request.Priority.Value, v => wp.Priority = v);

        if (request.Plan is not null)
            AuditAndSet(auditEntries, wp.Id, changedBy, now, "Plan", wp.Plan, request.Plan, v => wp.Plan = v);

        if (request.EstimatedComplexity is not null)
            AuditAndSetNullableInt(auditEntries, wp.Id, changedBy, now, "EstimatedComplexity", wp.EstimatedComplexity, request.EstimatedComplexity, v => wp.EstimatedComplexity = v);

        if (request.EstimationRationale is not null)
            AuditAndSet(auditEntries, wp.Id, changedBy, now, "EstimationRationale", wp.EstimationRationale, request.EstimationRationale, v => wp.EstimationRationale = v);

        if (request.State is not null)
            AuditAndSetEnum(auditEntries, wp.Id, changedBy, now, "State", wp.State, request.State.Value, v => wp.State = v);

        if (request.LinkedIssueId is not null)
            AuditAndSetNullableLong(auditEntries, wp.Id, changedBy, now, "LinkedIssueId", wp.LinkedIssueId, request.LinkedIssueId, v => wp.LinkedIssueId = v);

        if (request.Attachments is not null)
        {
            var oldJson = JsonSerializer.Serialize(wp.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description }));
            wp.Attachments = MapAttachments(request.Attachments);
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

            // Transitioning TO Blocked: capture previous active state
            if (newState == CompletionState.Blocked && CompletionStateConstants.ActiveStates.Contains(oldState))
                wp.PreviousActiveState = oldState;

            // Transitioning FROM Blocked: clear previous active state
            if (oldState == CompletionState.Blocked && newState != CompletionState.Blocked)
                wp.PreviousActiveState = null;

            ApplyStateTimestamps(wp, oldState, newState);
        }

        // Dep-completion auto-unblock: if WP transitioned to terminal, unblock dependents
        if (request.State is not null && oldState != request.State.Value
            && CompletionStateConstants.TerminalStates.Contains(request.State.Value))
        {
            await AutoUnblockDependentWpsAsync(wp, stateChanges, ct);
        }

        if (auditEntries.Count > 0)
            db.WorkPackageAuditLogs.AddRange(auditEntries);

        await db.SaveChangesAsync(ct);

        // Re-query with full tree for response
        var fullWp = await db.WorkPackages
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
            .Include(w => w.LinkedIssue)
            .FirstAsync(w => w.Id == wp.Id, ct);

        var response = ToResponse(fullWp);
        response.StateChanges = stateChanges.Count > 0 ? stateChanges : null;
        return response;
    }

    public async Task<bool> DeleteAsync(long projectId, int wpNumber, CancellationToken ct = default)
    {
        var wp = await db.WorkPackages
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkPackageNumber == wpNumber, ct);

        if (wp is null)
            return false;

        db.WorkPackages.Remove(wp);
        await db.SaveChangesAsync(ct);
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

        // Validate: no circular dependency (BFS from dependsOnWp through BlockedBy chain)
        if (await HasCircularDependencyAsync(dependentWp.Id, dependsOnWp.Id, ct))
            throw new InvalidOperationException("Adding this dependency would create a circular dependency.");

        var dependency = new WorkPackageDependency
        {
            DependentWorkPackageId = dependentWp.Id,
            DependsOnWorkPackageId = dependsOnWp.Id,
            Reason = request.Reason
        };

        db.WorkPackageDependencies.Add(dependency);

        // Auto-block: if dependent WP is in an active state and depends-on is non-terminal, transition to Blocked
        if (!CompletionStateConstants.TerminalStates.Contains(dependsOnWp.State)
            && dependentWp.State != CompletionState.Blocked
            && !CompletionStateConstants.TerminalStates.Contains(dependentWp.State)
            && !CompletionStateConstants.InactiveStates.Contains(dependentWp.State))
        {
            var oldState = dependentWp.State;
            dependentWp.PreviousActiveState = oldState;
            dependentWp.State = CompletionState.Blocked;
            ApplyStateTimestamps(dependentWp, oldState, CompletionState.Blocked);

            var now = DateTimeOffset.UtcNow;
            db.WorkPackageAuditLogs.Add(new WorkPackageAuditLog
            {
                WorkPackage = dependentWp,
                FieldName = "State",
                OldValue = oldState.ToString(),
                NewValue = CompletionState.Blocked.ToString(),
                ChangedBy = "system",
                ChangedAt = now
            });

            stateChanges?.Add(new StateChangeDto
            {
                EntityType = "WorkPackage",
                EntityId = $"proj-{dependentWp.ProjectId}-wp-{dependentWp.WorkPackageNumber}",
                OldState = oldState.ToString(),
                NewState = CompletionState.Blocked.ToString(),
                Reason = $"Auto-blocked: dependency on 'proj-{dependsOnWp.ProjectId}-wp-{dependsOnWp.WorkPackageNumber}' added"
            });
        }

        await db.SaveChangesAsync(ct);

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

                ApplyStateTimestamps(dependentWp, oldState, restoredState);

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
        return true;
    }

    // ── Private helpers ──

    private async Task AutoUnblockDependentWpsAsync(WorkPackage completedWp, List<StateChangeDto>? stateChanges, CancellationToken ct)
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
                ApplyStateTimestamps(dependentWp, oldState, restoredState);

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

    private static void ApplyStateTimestamps(WorkPackage wp, CompletionState oldState, CompletionState newState)
    {
        if (oldState == newState)
            return;

        var now = DateTimeOffset.UtcNow;

        // StartedAt: set once when entering an active state from inactive
        if (wp.StartedAt is null && CompletionStateConstants.ActiveStates.Contains(newState))
            wp.StartedAt = now;

        // CompletedAt: set when entering Completed, cleared when leaving terminal
        if (newState == CompletionState.Completed)
            wp.CompletedAt = now;
        else if (CompletionStateConstants.TerminalStates.Contains(oldState) && !CompletionStateConstants.TerminalStates.Contains(newState))
            wp.CompletedAt = null;

        // ResolvedAt: set when entering any terminal state, cleared when leaving terminal
        if (CompletionStateConstants.TerminalStates.Contains(newState))
            wp.ResolvedAt = now;
        else if (CompletionStateConstants.TerminalStates.Contains(oldState))
            wp.ResolvedAt = null;
    }

    private async Task<bool> HasCircularDependencyAsync(long dependentId, long dependsOnId, CancellationToken ct)
    {
        // BFS from dependsOnWp through its BlockedBy chain, checking if we reach dependentId
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

            var blockedByIds = await db.WorkPackageDependencies
                .Where(d => d.DependentWorkPackageId == currentId)
                .Select(d => d.DependsOnWorkPackageId)
                .ToListAsync(ct);

            foreach (var id in blockedByIds)
                queue.Enqueue(id);
        }

        return false;
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

    private static List<WorkPackageAuditLog> BuildCreateAuditEntries(WorkPackage wp, string changedBy)
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new List<WorkPackageAuditLog>();

        void Add(string field, string? value)
        {
            if (value is null) return;
            entries.Add(new WorkPackageAuditLog
            {
                WorkPackage = wp,
                FieldName = field,
                OldValue = null,
                NewValue = value,
                ChangedBy = changedBy,
                ChangedAt = now
            });
        }

        Add("Name", wp.Name);
        Add("Description", wp.Description);
        Add("Type", wp.Type.ToString());
        Add("Priority", wp.Priority.ToString());
        Add("Plan", wp.Plan);
        Add("EstimatedComplexity", wp.EstimatedComplexity?.ToString());
        Add("EstimationRationale", wp.EstimationRationale);
        Add("State", wp.State.ToString());
        Add("LinkedIssueId", wp.LinkedIssueId?.ToString());

        if (wp.Attachments.Count > 0)
            Add("Attachments", JsonSerializer.Serialize(wp.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description })));

        return entries;
    }

    private static void AuditAndSet(
        List<WorkPackageAuditLog> entries, long wpId, string changedBy, DateTimeOffset now,
        string field, string? oldValue, string newValue, Action<string> setter)
    {
        if (oldValue == newValue) return;
        entries.Add(new WorkPackageAuditLog
        {
            WorkPackageId = wpId,
            FieldName = field,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = changedBy,
            ChangedAt = now
        });
        setter(newValue);
    }

    private static void AuditAndSetEnum<TEnum>(
        List<WorkPackageAuditLog> entries, long wpId, string changedBy, DateTimeOffset now,
        string field, TEnum oldValue, TEnum newValue, Action<TEnum> setter) where TEnum : struct, Enum
    {
        if (EqualityComparer<TEnum>.Default.Equals(oldValue, newValue)) return;
        entries.Add(new WorkPackageAuditLog
        {
            WorkPackageId = wpId,
            FieldName = field,
            OldValue = oldValue.ToString(),
            NewValue = newValue.ToString(),
            ChangedBy = changedBy,
            ChangedAt = now
        });
        setter(newValue);
    }

    private static void AuditAndSetNullableInt(
        List<WorkPackageAuditLog> entries, long wpId, string changedBy, DateTimeOffset now,
        string field, int? oldValue, int? newValue, Action<int?> setter)
    {
        if (oldValue == newValue) return;
        entries.Add(new WorkPackageAuditLog
        {
            WorkPackageId = wpId,
            FieldName = field,
            OldValue = oldValue?.ToString(),
            NewValue = newValue?.ToString(),
            ChangedBy = changedBy,
            ChangedAt = now
        });
        setter(newValue);
    }

    private static void AuditAndSetNullableLong(
        List<WorkPackageAuditLog> entries, long wpId, string changedBy, DateTimeOffset now,
        string field, long? oldValue, long? newValue, Action<long?> setter)
    {
        if (oldValue == newValue) return;
        entries.Add(new WorkPackageAuditLog
        {
            WorkPackageId = wpId,
            FieldName = field,
            OldValue = oldValue?.ToString(),
            NewValue = newValue?.ToString(),
            ChangedBy = changedBy,
            ChangedAt = now
        });
        setter(newValue);
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
        LinkedIssueId = w.LinkedIssueId is not null ? $"proj-{w.ProjectId}-issue-{w.LinkedIssueId}" : null,
        StartedAt = w.StartedAt,
        CompletedAt = w.CompletedAt,
        ResolvedAt = w.ResolvedAt,
        Attachments = w.Attachments.Select(a => new FileReferenceDto
        {
            FileName = a.FileName,
            RelativePath = a.RelativePath,
            Description = a.Description
        }).ToList(),
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
        LinkedIssueId = w.LinkedIssue is not null
            ? $"proj-{w.ProjectId}-issue-{w.LinkedIssue.IssueNumber}"
            : (w.LinkedIssueId is not null ? w.LinkedIssueId.ToString() : null),
        StartedAt = w.StartedAt,
        CompletedAt = w.CompletedAt,
        ResolvedAt = w.ResolvedAt,
        Attachments = w.Attachments.Select(a => new FileReferenceDto
        {
            FileName = a.FileName,
            RelativePath = a.RelativePath,
            Description = a.Description
        }).ToList(),
        Phases = w.Phases.Select(p => new PhaseResponse
        {
            PhaseId = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}-phase-{p.PhaseNumber}",
            Id = p.Id,
            PhaseNumber = p.PhaseNumber,
            Name = p.Name,
            Description = p.Description,
            SortOrder = p.SortOrder,
            State = p.State.ToString(),
            Tasks = p.Tasks.Select(t => new TaskResponse
            {
                TaskId = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}-task-{t.TaskNumber}",
                Id = t.Id,
                TaskNumber = t.TaskNumber,
                PhaseId = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}-phase-{p.PhaseNumber}",
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
                    TaskId = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}-task-{d.DependsOnTask.TaskNumber}",
                    Name = d.DependsOnTask.Name,
                    State = d.DependsOnTask.State.ToString(),
                    Reason = d.Reason
                }).ToList(),
                Blocking = t.Blocking.Select(d => new TaskDependencyResponse
                {
                    TaskId = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}-task-{d.DependentTask.TaskNumber}",
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
        }).ToList(),
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
}
