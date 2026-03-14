using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class IssueService(AppDbContext db, IEventBroadcaster broadcaster) : IIssueService
{
    public async Task<List<IssueResponse>> GetByProjectAsync(
        long projectId, string? stateFilter, CancellationToken ct = default)
    {
        var query = db.Issues.Where(i => i.ProjectId == projectId);
        query = ApplyStateFilter(query, stateFilter);

        var responses = await query
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => ToResponse(i))
            .ToListAsync(ct);

        await EnrichWithLinkedWorkPackagesAsync(responses, projectId, ct);
        return responses;
    }

    public async Task<IssueResponse?> GetByNumberAsync(
        long projectId, int issueNumber, CancellationToken ct = default)
    {
        var issue = await db.Issues
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.IssueNumber == issueNumber, ct);

        if (issue is null)
            return null;

        var response = ToResponse(issue);
        await EnrichWithLinkedWorkPackagesAsync([response], projectId, ct);
        return response;
    }

    public async Task<IssueSummaryResponse> GetSummaryAsync(long projectId, CancellationToken ct = default)
    {
        var query = db.Issues.Where(i => i.ProjectId == projectId);

        var activeCount = await query.CountAsync(i => CompletionStateConstants.ActiveStates.Contains(i.State), ct);
        var inactiveCount = await query.CountAsync(i => CompletionStateConstants.InactiveStates.Contains(i.State), ct);
        var terminalCount = await query.CountAsync(i => CompletionStateConstants.TerminalStates.Contains(i.State), ct);

        var latestTerminal = await query
            .Where(i => CompletionStateConstants.TerminalStates.Contains(i.State))
            .OrderByDescending(i => i.ResolvedAt ?? i.UpdatedAt)
            .Take(10)
            .Select(i => ToResponse(i))
            .ToListAsync(ct);

        return new IssueSummaryResponse
        {
            ActiveCount = activeCount,
            InactiveCount = inactiveCount,
            TerminalCount = terminalCount,
            LatestTerminalIssues = latestTerminal
        };
    }

    public async Task<List<IssueAuditLogResponse>> GetAuditLogAsync(
        long projectId, int issueNumber, CancellationToken ct = default)
    {
        var issue = await db.Issues
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.IssueNumber == issueNumber, ct);

        if (issue is null)
            return [];

        return await db.IssueAuditLogs
            .Where(a => a.IssueId == issue.Id)
            .OrderByDescending(a => a.ChangedAt)
            .Select(a => new IssueAuditLogResponse
            {
                FieldName = a.FieldName,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                ChangedBy = a.ChangedBy,
                ChangedAt = a.ChangedAt
            })
            .ToListAsync(ct);
    }

    public async Task<IssueResponse> CreateAsync(
        long projectId, CreateIssueRequest request, string changedBy, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async (cancellation) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellation);

            var project = await db.Projects.FirstAsync(p => p.Id == projectId, cancellation);
            var nextNumber = project.NextIssueNumber;
            project.NextIssueNumber++;

            var issue = new Issue
            {
                IssueNumber = nextNumber,
                ProjectId = projectId,
                Name = request.Name,
                Description = request.Description,
                IssueType = request.IssueType,
                Severity = request.Severity,
                Priority = request.Priority,
                StepsToReproduce = request.StepsToReproduce,
                ExpectedBehavior = request.ExpectedBehavior,
                ActualBehavior = request.ActualBehavior,
                AffectedComponent = request.AffectedComponent,
                StackTrace = request.StackTrace,
                RootCause = request.RootCause,
                Resolution = request.Resolution,
                State = request.State,
                Attachments = StateTransitionHelper.MapFileReferences(request.Attachments)
            };

            // Apply state-driven timestamps
            StateTransitionHelper.ApplyStateTimestamps(issue, CompletionState.NotStarted, request.State);

            db.Issues.Add(issue);

            // Audit all fields on creation
            var auditEntries = new List<IssueAuditLog>();
            var createAudit = () => new IssueAuditLog { Issue = issue, FieldName = default!, ChangedBy = changedBy, ChangedAt = DateTimeOffset.UtcNow };
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Name", issue.Name);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Description", issue.Description);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "IssueType", issue.IssueType.ToString());
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Severity", issue.Severity.ToString());
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Priority", issue.Priority.ToString());
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "State", issue.State.ToString());
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "StepsToReproduce", issue.StepsToReproduce);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "ExpectedBehavior", issue.ExpectedBehavior);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "ActualBehavior", issue.ActualBehavior);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "AffectedComponent", issue.AffectedComponent);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "StackTrace", issue.StackTrace);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "RootCause", issue.RootCause);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Resolution", issue.Resolution);
            if (issue.Attachments.Count > 0)
                AuditHelper.AddCreateEntry(auditEntries, createAudit, "Attachments", JsonSerializer.Serialize(issue.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description })));
            db.IssueAuditLogs.AddRange(auditEntries);

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);

            broadcaster.Publish(new ServerEvent
            {
                EventType = "entity:changed",
                EntityType = "Issue",
                EntityId = $"proj-{projectId}-issue-{issue.IssueNumber}",
                Action = "created",
                ProjectId = projectId
            });

            return ToResponse(issue);
        }, ct);
    }

    public async Task<IssueResponse?> UpdateAsync(
        long projectId, int issueNumber, UpdateIssueRequest request, string changedBy, CancellationToken ct = default)
    {
        var issue = await db.Issues
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.IssueNumber == issueNumber, ct);

        if (issue is null)
            return null;

        var auditEntries = new List<IssueAuditLog>();
        var now = DateTimeOffset.UtcNow;
        var audit = () => new IssueAuditLog { IssueId = issue.Id, FieldName = default!, ChangedBy = changedBy, ChangedAt = now };

        // Track state before changes for timestamp logic
        var oldState = issue.State;

        if (request.Name is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "Name", issue.Name, request.Name, v => issue.Name = v);

        if (request.Description is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "Description", issue.Description, request.Description, v => issue.Description = v);

        if (request.IssueType is not null)
            AuditHelper.AuditAndSetEnum(auditEntries, audit, "IssueType", issue.IssueType, request.IssueType.Value, v => issue.IssueType = v);

        if (request.Severity is not null)
            AuditHelper.AuditAndSetEnum(auditEntries, audit, "Severity", issue.Severity, request.Severity.Value, v => issue.Severity = v);

        if (request.Priority is not null)
            AuditHelper.AuditAndSetEnum(auditEntries, audit, "Priority", issue.Priority, request.Priority.Value, v => issue.Priority = v);

        if (request.StepsToReproduce is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "StepsToReproduce", issue.StepsToReproduce, request.StepsToReproduce, v => issue.StepsToReproduce = v);

        if (request.ExpectedBehavior is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "ExpectedBehavior", issue.ExpectedBehavior, request.ExpectedBehavior, v => issue.ExpectedBehavior = v);

        if (request.ActualBehavior is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "ActualBehavior", issue.ActualBehavior, request.ActualBehavior, v => issue.ActualBehavior = v);

        if (request.AffectedComponent is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "AffectedComponent", issue.AffectedComponent, request.AffectedComponent, v => issue.AffectedComponent = v);

        if (request.StackTrace is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "StackTrace", issue.StackTrace, request.StackTrace, v => issue.StackTrace = v);

        if (request.RootCause is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "RootCause", issue.RootCause, request.RootCause, v => issue.RootCause = v);

        if (request.Resolution is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "Resolution", issue.Resolution, request.Resolution, v => issue.Resolution = v);

        if (request.State is not null)
            AuditHelper.AuditAndSetEnum(auditEntries, audit, "State", issue.State, request.State.Value, v => issue.State = v);

        if (request.Attachments is not null)
        {
            var oldJson = JsonSerializer.Serialize(issue.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description }));
            issue.Attachments = StateTransitionHelper.MapFileReferences(request.Attachments);
            var newJson = JsonSerializer.Serialize(issue.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description }));

            if (oldJson != newJson)
            {
                auditEntries.Add(new IssueAuditLog
                {
                    IssueId = issue.Id,
                    FieldName = "Attachments",
                    OldValue = oldJson,
                    NewValue = newJson,
                    ChangedBy = changedBy,
                    ChangedAt = now
                });
            }
        }

        // Apply state-driven timestamps if state changed
        if (request.State is not null && oldState != request.State.Value)
            StateTransitionHelper.ApplyStateTimestamps(issue, oldState, request.State.Value);

        if (auditEntries.Count > 0)
            db.IssueAuditLogs.AddRange(auditEntries);

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "Issue",
            EntityId = $"proj-{projectId}-issue-{issueNumber}",
            Action = "updated",
            ProjectId = projectId
        });

        return ToResponse(issue);
    }

    public async Task<bool> DeleteAsync(long projectId, int issueNumber, CancellationToken ct = default)
    {
        var issue = await db.Issues
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.IssueNumber == issueNumber, ct);

        if (issue is null)
            return false;

        db.Issues.Remove(issue);
        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "Issue",
            EntityId = $"proj-{projectId}-issue-{issue.IssueNumber}",
            Action = "deleted",
            ProjectId = projectId
        });

        return true;
    }

    // ── Private helpers ──

    private async Task EnrichWithLinkedWorkPackagesAsync(
        List<IssueResponse> responses, long projectId, CancellationToken ct)
    {
        if (responses.Count == 0)
            return;

        var issueIds = responses.Select(r => r.Id).ToHashSet();

        var linkedWps = await db.WorkPackageIssueLinks
            .Where(l => issueIds.Contains(l.IssueId))
            .Include(l => l.WorkPackage)
            .Select(l => new
            {
                l.IssueId,
                l.WorkPackage.ProjectId,
                l.WorkPackage.WorkPackageNumber,
                l.WorkPackage.Name,
                State = l.WorkPackage.State.ToString(),
                Type = l.WorkPackage.Type.ToString(),
                Priority = l.WorkPackage.Priority.ToString()
            })
            .ToListAsync(ct);

        if (linkedWps.Count == 0)
            return;

        var lookup = linkedWps.ToLookup(wp => wp.IssueId);

        foreach (var response in responses)
        {
            var wps = lookup[response.Id];
            response.LinkedWorkPackages = wps.Select(wp => new LinkedWorkPackageItem
            {
                WorkPackageId = $"proj-{wp.ProjectId}-wp-{wp.WorkPackageNumber}",
                Name = wp.Name,
                State = wp.State,
                Type = wp.Type,
                Priority = wp.Priority
            }).ToList();
        }
    }

    private static IQueryable<Issue> ApplyStateFilter(IQueryable<Issue> query, string? stateFilter)
    {
        if (string.IsNullOrWhiteSpace(stateFilter))
            return query;

        if (stateFilter.Equals("open", StringComparison.OrdinalIgnoreCase))
            return query.Where(i => !CompletionStateConstants.TerminalStates.Contains(i.State));

        var states = stateFilter.ToLowerInvariant() switch
        {
            "active" => CompletionStateConstants.ActiveStates,
            "inactive" => CompletionStateConstants.InactiveStates,
            "terminal" => CompletionStateConstants.TerminalStates,
            _ => null
        };

        return states is null ? query : query.Where(i => states.Contains(i.State));
    }

    private static IssueResponse ToResponse(Issue i) => new()
    {
        IssueId = $"proj-{i.ProjectId}-issue-{i.IssueNumber}",
        Id = i.Id,
        IssueNumber = i.IssueNumber,
        ProjectId = $"proj-{i.ProjectId}",
        Name = i.Name,
        Description = i.Description,
        IssueType = i.IssueType.ToString(),
        Severity = i.Severity.ToString(),
        Priority = i.Priority.ToString(),
        StepsToReproduce = i.StepsToReproduce,
        ExpectedBehavior = i.ExpectedBehavior,
        ActualBehavior = i.ActualBehavior,
        AffectedComponent = i.AffectedComponent,
        StackTrace = i.StackTrace,
        RootCause = i.RootCause,
        Resolution = i.Resolution,
        State = i.State.ToString(),
        StartedAt = i.StartedAt,
        CompletedAt = i.CompletedAt,
        ResolvedAt = i.ResolvedAt,
        Attachments = i.Attachments.Select(a => new FileReferenceDto
        {
            FileName = a.FileName,
            RelativePath = a.RelativePath,
            Description = a.Description
        }).ToList(),
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt
    };
}
