using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class FeatureRequestService(AppDbContext db, IEventBroadcaster broadcaster) : IFeatureRequestService
{
    public async Task<List<FeatureRequestResponse>> GetByProjectAsync(
        long projectId, string? stateFilter, CancellationToken ct = default)
    {
        var query = db.FeatureRequests
            .Where(fr => fr.ProjectId == projectId)
            .OrderByDescending(fr => fr.CreatedAt)
            .AsQueryable();

        query = ApplyStateFilter(query, stateFilter);

        var featureRequests = await query.ToListAsync(ct);
        var responses = featureRequests.Select(ToResponse).ToList();
        await EnrichWithLinkedWorkPackagesAsync(responses, ct);
        return responses;
    }

    public async Task<FeatureRequestResponse?> GetByNumberAsync(
        long projectId, int frNumber, CancellationToken ct = default)
    {
        var fr = await db.FeatureRequests
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.FeatureRequestNumber == frNumber, ct);

        if (fr is null) return null;

        var response = ToResponse(fr);
        await EnrichWithLinkedWorkPackagesAsync([response], ct);
        return response;
    }

    public async Task<FeatureRequestResponse> CreateAsync(
        long projectId, CreateFeatureRequestRequest request, string changedBy, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async (cancellation) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellation);

            var nextNumber = await db.FeatureRequests
                .Where(fr => fr.ProjectId == projectId)
                .MaxAsync(fr => (int?)fr.FeatureRequestNumber, cancellation) ?? 0;
            nextNumber++;

            var fr = new FeatureRequest
            {
                FeatureRequestNumber = nextNumber,
                ProjectId = projectId,
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                Priority = request.Priority,
                Status = request.Status,
                BusinessValue = request.BusinessValue,
                UserStories = MapUserStories(request.UserStories),
                Requester = request.Requester,
                AcceptanceSummary = request.AcceptanceSummary,
                Attachments = StateTransitionHelper.MapFileReferences(request.Attachments)
            };

            StateTransitionHelper.ApplyFeatureStatusTimestamps(fr, FeatureStatus.Proposed, request.Status);

            db.FeatureRequests.Add(fr);

            var auditEntries = new List<FeatureRequestAuditLog>();
            var createAudit = () => new FeatureRequestAuditLog { FeatureRequest = fr, FieldName = default!, ChangedBy = changedBy, ChangedAt = DateTimeOffset.UtcNow };
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Name", fr.Name);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Description", fr.Description);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Category", fr.Category.ToString());
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Priority", fr.Priority.ToString());
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Status", fr.Status.ToString());
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "BusinessValue", fr.BusinessValue);
            if (fr.UserStories.Count > 0)
                AuditHelper.AddCreateEntry(auditEntries, createAudit, "UserStories", JsonSerializer.Serialize(fr.UserStories.Select(us => new { us.Role, us.Goal, us.Benefit })));
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "Requester", fr.Requester);
            AuditHelper.AddCreateEntry(auditEntries, createAudit, "AcceptanceSummary", fr.AcceptanceSummary);
            if (fr.Attachments.Count > 0)
                AuditHelper.AddCreateEntry(auditEntries, createAudit, "Attachments", JsonSerializer.Serialize(fr.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description })));
            db.FeatureRequestAuditLogs.AddRange(auditEntries);

            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);

            broadcaster.Publish(new ServerEvent
            {
                EventType = "entity:changed",
                EntityType = "FeatureRequest",
                EntityId = $"proj-{projectId}-fr-{fr.FeatureRequestNumber}",
                Action = "created",
                ProjectId = projectId
            });

            return ToResponse(fr);
        }, ct);
    }

    public async Task<FeatureRequestResponse?> UpdateAsync(
        long projectId, int frNumber, UpdateFeatureRequestRequest request, string changedBy, CancellationToken ct = default)
    {
        var fr = await db.FeatureRequests
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.FeatureRequestNumber == frNumber, ct);

        if (fr is null) return null;

        var auditEntries = new List<FeatureRequestAuditLog>();
        var now = DateTimeOffset.UtcNow;
        var audit = () => new FeatureRequestAuditLog { FeatureRequestId = fr.Id, FieldName = default!, ChangedBy = changedBy, ChangedAt = now };
        var oldStatus = fr.Status;

        if (request.Name is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "Name", fr.Name, request.Name, v => fr.Name = v);

        if (request.Description is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "Description", fr.Description, request.Description, v => fr.Description = v);

        if (request.Category is not null)
            AuditHelper.AuditAndSetEnum(auditEntries, audit, "Category", fr.Category, request.Category.Value, v => fr.Category = v);

        if (request.Priority is not null)
            AuditHelper.AuditAndSetEnum(auditEntries, audit, "Priority", fr.Priority, request.Priority.Value, v => fr.Priority = v);

        if (request.Status is not null)
            AuditHelper.AuditAndSetEnum(auditEntries, audit, "Status", fr.Status, request.Status.Value, v => fr.Status = v);

        if (request.BusinessValue is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "BusinessValue", fr.BusinessValue, request.BusinessValue, v => fr.BusinessValue = v);

        if (request.Requester is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "Requester", fr.Requester, request.Requester, v => fr.Requester = v);

        if (request.AcceptanceSummary is not null)
            AuditHelper.AuditAndSet(auditEntries, audit, "AcceptanceSummary", fr.AcceptanceSummary, request.AcceptanceSummary, v => fr.AcceptanceSummary = v);

        if (request.Attachments is not null)
        {
            var oldJson = JsonSerializer.Serialize(fr.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description }));
            fr.Attachments = StateTransitionHelper.MapFileReferences(request.Attachments);
            var newJson = JsonSerializer.Serialize(fr.Attachments.Select(a => new { a.FileName, a.RelativePath, a.Description }));
            if (oldJson != newJson)
            {
                auditEntries.Add(new FeatureRequestAuditLog
                {
                    FeatureRequestId = fr.Id,
                    FieldName = "Attachments",
                    OldValue = oldJson,
                    NewValue = newJson,
                    ChangedBy = changedBy,
                    ChangedAt = now
                });
            }
        }

        if (request.Status is not null && oldStatus != request.Status.Value)
            StateTransitionHelper.ApplyFeatureStatusTimestamps(fr, oldStatus, request.Status.Value);

        if (auditEntries.Count > 0)
            db.FeatureRequestAuditLogs.AddRange(auditEntries);

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "FeatureRequest",
            EntityId = $"proj-{projectId}-fr-{frNumber}",
            Action = "updated",
            ProjectId = projectId
        });

        var response = ToResponse(fr);
        await EnrichWithLinkedWorkPackagesAsync([response], ct);
        return response;
    }

    public async Task<bool> DeleteAsync(long projectId, int frNumber, CancellationToken ct = default)
    {
        var fr = await db.FeatureRequests
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.FeatureRequestNumber == frNumber, ct);

        if (fr is null) return false;

        db.FeatureRequests.Remove(fr);
        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "FeatureRequest",
            EntityId = $"proj-{projectId}-fr-{fr.FeatureRequestNumber}",
            Action = "deleted",
            ProjectId = projectId
        });

        return true;
    }

    public async Task<FeatureRequestResponse?> ManageUserStoriesAsync(
        long projectId, int frNumber, ManageUserStoriesRequest request, string changedBy, CancellationToken ct = default)
    {
        var fr = await db.FeatureRequests
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.FeatureRequestNumber == frNumber, ct);

        if (fr is null) return null;

        var now = DateTimeOffset.UtcNow;
        var oldJson = JsonSerializer.Serialize(fr.UserStories.Select(us => new { us.Role, us.Goal, us.Benefit }));

        switch (request.Action.ToLowerInvariant())
        {
            case "add":
                if (string.IsNullOrWhiteSpace(request.Role) || string.IsNullOrWhiteSpace(request.Goal) || string.IsNullOrWhiteSpace(request.Benefit))
                    throw new ArgumentException("Role, Goal, and Benefit are required for Add action.");
                fr.UserStories.Add(new UserStory
                {
                    Role = request.Role,
                    Goal = request.Goal,
                    Benefit = request.Benefit
                });
                break;

            case "update":
                if (request.Index is null || request.Index < 0 || request.Index >= fr.UserStories.Count)
                    throw new ArgumentOutOfRangeException(nameof(request.Index),
                        $"Index must be between 0 and {fr.UserStories.Count - 1}.");
                if (string.IsNullOrWhiteSpace(request.Role) || string.IsNullOrWhiteSpace(request.Goal) || string.IsNullOrWhiteSpace(request.Benefit))
                    throw new ArgumentException("Role, Goal, and Benefit are required for Update action.");
                fr.UserStories[request.Index.Value] = new UserStory
                {
                    Role = request.Role,
                    Goal = request.Goal,
                    Benefit = request.Benefit
                };
                break;

            case "remove":
                if (request.Index is null || request.Index < 0 || request.Index >= fr.UserStories.Count)
                    throw new ArgumentOutOfRangeException(nameof(request.Index),
                        $"Index must be between 0 and {fr.UserStories.Count - 1}.");
                fr.UserStories.RemoveAt(request.Index.Value);
                break;

            default:
                throw new ArgumentException($"Invalid action '{request.Action}'. Must be Add, Update, or Remove.");
        }

        var newJson = JsonSerializer.Serialize(fr.UserStories.Select(us => new { us.Role, us.Goal, us.Benefit }));
        if (oldJson != newJson)
        {
            db.FeatureRequestAuditLogs.Add(new FeatureRequestAuditLog
            {
                FeatureRequestId = fr.Id,
                FieldName = "UserStories",
                OldValue = oldJson,
                NewValue = newJson,
                ChangedBy = changedBy,
                ChangedAt = now
            });
        }

        await db.SaveChangesAsync(ct);

        broadcaster.Publish(new ServerEvent
        {
            EventType = "entity:changed",
            EntityType = "FeatureRequest",
            EntityId = $"proj-{projectId}-fr-{frNumber}",
            Action = "updated",
            ProjectId = projectId
        });

        var response = ToResponse(fr);
        await EnrichWithLinkedWorkPackagesAsync([response], ct);
        return response;
    }

    // ── Private helpers ──

    private async Task EnrichWithLinkedWorkPackagesAsync(
        List<FeatureRequestResponse> responses, CancellationToken ct)
    {
        if (responses.Count == 0) return;

        var frIds = responses.Select(r => r.Id).ToHashSet();

        var linkedWps = await db.WorkPackages
            .Where(w => w.LinkedFeatureRequestId != null && frIds.Contains(w.LinkedFeatureRequestId.Value))
            .Select(w => new
            {
                w.LinkedFeatureRequestId,
                w.ProjectId,
                w.WorkPackageNumber,
                w.Name,
                w.State,
                w.Type,
                w.Priority
            })
            .ToListAsync(ct);

        var lookup = linkedWps.ToLookup(w => w.LinkedFeatureRequestId!.Value);

        foreach (var response in responses)
        {
            response.LinkedWorkPackages = lookup[response.Id]
                .Select(w => new LinkedWorkPackageItem
                {
                    WorkPackageId = $"proj-{w.ProjectId}-wp-{w.WorkPackageNumber}",
                    Name = w.Name,
                    State = w.State.ToString(),
                    Type = w.Type.ToString(),
                    Priority = w.Priority.ToString()
                })
                .ToList();
        }
    }

    private static IQueryable<FeatureRequest> ApplyStateFilter(IQueryable<FeatureRequest> query, string? stateFilter)
    {
        return stateFilter?.ToLowerInvariant() switch
        {
            "active" => query.Where(fr => FeatureStatusConstants.ActiveStates.Contains(fr.Status)),
            "inactive" => query.Where(fr => FeatureStatusConstants.InactiveStates.Contains(fr.Status)),
            "terminal" => query.Where(fr => FeatureStatusConstants.TerminalStates.Contains(fr.Status)),
            _ => query
        };
    }

    private static List<UserStory> MapUserStories(List<UserStoryDto>? dtos) =>
        dtos?.Select(us => new UserStory
        {
            Role = us.Role,
            Goal = us.Goal,
            Benefit = us.Benefit
        }).ToList() ?? [];

    private static FeatureRequestResponse ToResponse(FeatureRequest fr) => new()
    {
        FeatureRequestId = $"proj-{fr.ProjectId}-fr-{fr.FeatureRequestNumber}",
        Id = fr.Id,
        FeatureRequestNumber = fr.FeatureRequestNumber,
        ProjectId = $"proj-{fr.ProjectId}",
        Name = fr.Name,
        Description = fr.Description,
        Category = fr.Category.ToString(),
        Priority = fr.Priority.ToString(),
        Status = fr.Status.ToString(),
        BusinessValue = fr.BusinessValue,
        UserStories = fr.UserStories.Select(us => new UserStoryDto
        {
            Role = us.Role,
            Goal = us.Goal,
            Benefit = us.Benefit
        }).ToList(),
        Requester = fr.Requester,
        AcceptanceSummary = fr.AcceptanceSummary,
        StartedAt = fr.StartedAt,
        CompletedAt = fr.CompletedAt,
        ResolvedAt = fr.ResolvedAt,
        Attachments = fr.Attachments.Select(a => new FileReferenceDto
        {
            FileName = a.FileName,
            RelativePath = a.RelativePath,
            Description = a.Description
        }).ToList(),
        CreatedAt = fr.CreatedAt,
        UpdatedAt = fr.UpdatedAt
    };
}
