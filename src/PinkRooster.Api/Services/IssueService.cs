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
        var issues = await db.Issues
            .Where(i => i.ProjectId == projectId)
            .ToListAsync(ct);

        var activeCount = issues.Count(i => CompletionStateConstants.ActiveStates.Contains(i.State));
        var inactiveCount = issues.Count(i => CompletionStateConstants.InactiveStates.Contains(i.State));
        var terminalCount = issues.Count(i => CompletionStateConstants.TerminalStates.Contains(i.State));

        var latestTerminal = issues
            .Where(i => CompletionStateConstants.TerminalStates.Contains(i.State))
            .OrderByDescending(i => i.ResolvedAt ?? i.UpdatedAt)
            .Take(10)
            .Select(i => ToResponse(i))
            .ToList();

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

            var nextNumber = await db.Issues
                .Where(i => i.ProjectId == projectId)
                .MaxAsync(i => (int?)i.IssueNumber, cancellation) ?? 0;
            nextNumber++;

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
            var auditEntries = BuildCreateAuditEntries(issue, changedBy);
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

        // Track state before changes for timestamp logic
        var oldState = issue.State;

        if (request.Name is not null)
            AuditAndSet(auditEntries, issue.Id, changedBy, now, "Name", issue.Name, request.Name, v => issue.Name = v);

        if (request.Description is not null)
            AuditAndSet(auditEntries, issue.Id, changedBy, now, "Description", issue.Description, request.Description, v => issue.Description = v);

        if (request.IssueType is not null)
            AuditAndSetEnum(auditEntries, issue.Id, changedBy, now, "IssueType", issue.IssueType, request.IssueType.Value, v => issue.IssueType = v);

        if (request.Severity is not null)
            AuditAndSetEnum(auditEntries, issue.Id, changedBy, now, "Severity", issue.Severity, request.Severity.Value, v => issue.Severity = v);

        if (request.Priority is not null)
            AuditAndSetEnum(auditEntries, issue.Id, changedBy, now, "Priority", issue.Priority, request.Priority.Value, v => issue.Priority = v);

        if (request.StepsToReproduce is not null)
            AuditAndSet(auditEntries, issue.Id, changedBy, now, "StepsToReproduce", issue.StepsToReproduce, request.StepsToReproduce, v => issue.StepsToReproduce = v);

        if (request.ExpectedBehavior is not null)
            AuditAndSet(auditEntries, issue.Id, changedBy, now, "ExpectedBehavior", issue.ExpectedBehavior, request.ExpectedBehavior, v => issue.ExpectedBehavior = v);

        if (request.ActualBehavior is not null)
            AuditAndSet(auditEntries, issue.Id, changedBy, now, "ActualBehavior", issue.ActualBehavior, request.ActualBehavior, v => issue.ActualBehavior = v);

        if (request.AffectedComponent is not null)
            AuditAndSet(auditEntries, issue.Id, changedBy, now, "AffectedComponent", issue.AffectedComponent, request.AffectedComponent, v => issue.AffectedComponent = v);

        if (request.StackTrace is not null)
            AuditAndSet(auditEntries, issue.Id, changedBy, now, "StackTrace", issue.StackTrace, request.StackTrace, v => issue.StackTrace = v);

        if (request.RootCause is not null)
            AuditAndSet(auditEntries, issue.Id, changedBy, now, "RootCause", issue.RootCause, request.RootCause, v => issue.RootCause = v);

        if (request.Resolution is not null)
            AuditAndSet(auditEntries, issue.Id, changedBy, now, "Resolution", issue.Resolution, request.Resolution, v => issue.Resolution = v);

        if (request.State is not null)
            AuditAndSetEnum(auditEntries, issue.Id, changedBy, now, "State", issue.State, request.State.Value, v => issue.State = v);

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

        var linkedWps = await db.WorkPackages
            .Where(wp => wp.ProjectId == projectId && wp.LinkedIssueId != null && issueIds.Contains(wp.LinkedIssueId.Value))
            .Select(wp => new
            {
                wp.LinkedIssueId,
                wp.ProjectId,
                wp.WorkPackageNumber,
                wp.Name,
                State = wp.State.ToString(),
                Type = wp.Type.ToString(),
                Priority = wp.Priority.ToString()
            })
            .ToListAsync(ct);

        if (linkedWps.Count == 0)
            return;

        var lookup = linkedWps.ToLookup(wp => wp.LinkedIssueId!.Value);

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

        var states = stateFilter.ToLowerInvariant() switch
        {
            "active" => CompletionStateConstants.ActiveStates,
            "inactive" => CompletionStateConstants.InactiveStates,
            "terminal" => CompletionStateConstants.TerminalStates,
            _ => null
        };

        return states is null ? query : query.Where(i => states.Contains(i.State));
    }

    private static List<IssueAuditLog> BuildCreateAuditEntries(Issue issue, string changedBy)
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new List<IssueAuditLog>();

        void Add(string field, string? value)
        {
            if (value is null) return;
            entries.Add(new IssueAuditLog
            {
                Issue = issue,
                FieldName = field,
                OldValue = null,
                NewValue = value,
                ChangedBy = changedBy,
                ChangedAt = now
            });
        }

        Add("Name", issue.Name);
        Add("Description", issue.Description);
        Add("IssueType", issue.IssueType.ToString());
        Add("Severity", issue.Severity.ToString());
        Add("Priority", issue.Priority.ToString());
        Add("State", issue.State.ToString());
        Add("StepsToReproduce", issue.StepsToReproduce);
        Add("ExpectedBehavior", issue.ExpectedBehavior);
        Add("ActualBehavior", issue.ActualBehavior);
        Add("AffectedComponent", issue.AffectedComponent);
        Add("StackTrace", issue.StackTrace);
        Add("RootCause", issue.RootCause);
        Add("Resolution", issue.Resolution);

        if (issue.Attachments.Count > 0)
            Add("Attachments", JsonSerializer.Serialize(issue.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description })));

        return entries;
    }

    private static void AuditAndSet(
        List<IssueAuditLog> entries, long issueId, string changedBy, DateTimeOffset now,
        string field, string? oldValue, string newValue, Action<string> setter)
    {
        if (oldValue == newValue) return;
        entries.Add(new IssueAuditLog
        {
            IssueId = issueId,
            FieldName = field,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = changedBy,
            ChangedAt = now
        });
        setter(newValue);
    }

    private static void AuditAndSetEnum<TEnum>(
        List<IssueAuditLog> entries, long issueId, string changedBy, DateTimeOffset now,
        string field, TEnum oldValue, TEnum newValue, Action<TEnum> setter) where TEnum : struct, Enum
    {
        if (EqualityComparer<TEnum>.Default.Equals(oldValue, newValue)) return;
        entries.Add(new IssueAuditLog
        {
            IssueId = issueId,
            FieldName = field,
            OldValue = oldValue.ToString(),
            NewValue = newValue.ToString(),
            ChangedBy = changedBy,
            ChangedAt = now
        });
        setter(newValue);
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
