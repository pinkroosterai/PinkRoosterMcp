namespace PinkRooster.Data.Entities;

public sealed class WebhookDeliveryLog
{
    public long Id { get; set; }
    public long WebhookSubscriptionId { get; set; }
    public WebhookSubscription Subscription { get; set; } = null!;

    // ── Event Context ──
    public required string EventType { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }

    // ── Delivery Attempt ──
    public int AttemptNumber { get; set; } = 1;
    public required string Payload { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public int DurationMs { get; set; }
    public bool Success { get; set; }

    // ── Retry ──
    public DateTimeOffset? NextRetryAt { get; set; }

    // ── Timestamps ──
    public DateTimeOffset CreatedAt { get; set; }
}
