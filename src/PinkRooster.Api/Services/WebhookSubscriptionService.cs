using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public sealed class WebhookSubscriptionService(AppDbContext db) : IWebhookSubscriptionService
{
    public async Task<List<WebhookSubscriptionResponse>> GetByProjectAsync(long projectId, CancellationToken ct = default)
    {
        return await db.WebhookSubscriptions
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => MapToResponse(s))
            .ToListAsync(ct);
    }

    public async Task<WebhookSubscriptionResponse?> GetByIdAsync(long projectId, long subscriptionId, CancellationToken ct = default)
    {
        var sub = await db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.ProjectId == projectId, ct);

        return sub is null ? null : MapToResponse(sub);
    }

    public async Task<WebhookSubscriptionResponse> CreateAsync(long projectId, CreateWebhookSubscriptionRequest request, CancellationToken ct = default)
    {
        var sub = new WebhookSubscription
        {
            ProjectId = projectId,
            Url = request.Url,
            Secret = request.Secret,
            IsActive = true,
            EventFilters = request.EventFilters?.Select(f => new WebhookEventFilter
            {
                EventType = f.EventType,
                EntityType = f.EntityType
            }).ToList() ?? []
        };

        db.WebhookSubscriptions.Add(sub);
        await db.SaveChangesAsync(ct);

        return MapToResponse(sub);
    }

    public async Task<WebhookSubscriptionResponse?> UpdateAsync(long projectId, long subscriptionId,
        UpdateWebhookSubscriptionRequest request, CancellationToken ct = default)
    {
        var sub = await db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.ProjectId == projectId, ct);

        if (sub is null) return null;

        if (request.Url is not null) sub.Url = request.Url;
        if (request.Secret is not null) sub.Secret = request.Secret;
        if (request.IsActive.HasValue) sub.IsActive = request.IsActive.Value;
        if (request.EventFilters is not null)
        {
            sub.EventFilters = request.EventFilters.Select(f => new WebhookEventFilter
            {
                EventType = f.EventType,
                EntityType = f.EntityType
            }).ToList();
        }

        await db.SaveChangesAsync(ct);
        return MapToResponse(sub);
    }

    public async Task<bool> DeleteAsync(long projectId, long subscriptionId, CancellationToken ct = default)
    {
        var sub = await db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.ProjectId == projectId, ct);

        if (sub is null) return false;

        db.WebhookSubscriptions.Remove(sub);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<WebhookDeliveryLogResponse>> GetDeliveryLogsAsync(long projectId, long subscriptionId,
        int limit = 50, CancellationToken ct = default)
    {
        return await db.WebhookDeliveryLogs
            .Where(l => l.Subscription.ProjectId == projectId && l.WebhookSubscriptionId == subscriptionId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .Select(l => new WebhookDeliveryLogResponse
            {
                Id = l.Id,
                WebhookSubscriptionId = l.WebhookSubscriptionId,
                EventType = l.EventType,
                EntityType = l.EntityType,
                EntityId = l.EntityId,
                AttemptNumber = l.AttemptNumber,
                HttpStatusCode = l.HttpStatusCode,
                DurationMs = l.DurationMs,
                Success = l.Success,
                NextRetryAt = l.NextRetryAt,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(ct);
    }

    private static WebhookSubscriptionResponse MapToResponse(WebhookSubscription sub) => new()
    {
        Id = sub.Id,
        ProjectId = sub.ProjectId,
        Url = sub.Url,
        IsActive = sub.IsActive,
        EventFilters = sub.EventFilters.Select(f => new WebhookEventFilterDto
        {
            EventType = f.EventType,
            EntityType = f.EntityType
        }).ToList(),
        LastDeliveredAt = sub.LastDeliveredAt,
        LastFailedAt = sub.LastFailedAt,
        ConsecutiveFailures = sub.ConsecutiveFailures,
        CreatedAt = sub.CreatedAt,
        UpdatedAt = sub.UpdatedAt
    };
}
