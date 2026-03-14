namespace PinkRooster.Shared.DTOs.Requests;

public sealed class CreateWebhookSubscriptionRequest
{
    public required string Url { get; init; }
    public required string Secret { get; init; }
    public List<WebhookEventFilterDto>? EventFilters { get; init; }
}

public sealed class UpdateWebhookSubscriptionRequest
{
    public string? Url { get; init; }
    public string? Secret { get; init; }
    public bool? IsActive { get; init; }
    public List<WebhookEventFilterDto>? EventFilters { get; init; }
}

public sealed class WebhookEventFilterDto
{
    public required string EventType { get; init; }
    public string? EntityType { get; init; }
}
