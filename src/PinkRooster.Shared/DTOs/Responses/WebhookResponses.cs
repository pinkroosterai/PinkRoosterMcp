using PinkRooster.Shared.DTOs.Requests;

namespace PinkRooster.Shared.DTOs.Responses;

public sealed class WebhookSubscriptionResponse
{
    public long Id { get; init; }
    public long ProjectId { get; init; }
    public required string Url { get; init; }
    public bool IsActive { get; init; }
    public List<WebhookEventFilterDto> EventFilters { get; init; } = [];
    public DateTimeOffset? LastDeliveredAt { get; init; }
    public DateTimeOffset? LastFailedAt { get; init; }
    public int ConsecutiveFailures { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class WebhookDeliveryLogResponse
{
    public long Id { get; init; }
    public long WebhookSubscriptionId { get; init; }
    public required string EventType { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public int AttemptNumber { get; init; }
    public int? HttpStatusCode { get; init; }
    public int DurationMs { get; init; }
    public bool Success { get; init; }
    public DateTimeOffset? NextRetryAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
