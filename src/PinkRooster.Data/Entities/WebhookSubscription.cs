namespace PinkRooster.Data.Entities;

public sealed class WebhookSubscription : IHasUpdatedAt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    // ── Configuration ──
    public required string Url { get; set; }
    public required string Secret { get; set; }
    public bool IsActive { get; set; } = true;

    // ── Event Filters (jsonb) ──
    public List<WebhookEventFilter> EventFilters { get; set; } = [];

    // ── Delivery State ──
    public DateTimeOffset? LastDeliveredAt { get; set; }
    public DateTimeOffset? LastFailedAt { get; set; }
    public int ConsecutiveFailures { get; set; }

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // ── Navigation ──
    public List<WebhookDeliveryLog> DeliveryLogs { get; set; } = [];
}

public sealed class WebhookEventFilter
{
    public required string EventType { get; set; }
    public string? EntityType { get; set; }
}
