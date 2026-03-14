namespace PinkRooster.Api.Services;

public interface IWebhookService
{
    Task EnqueueDeliveryAsync(long projectId, string eventType, string entityType, string entityId,
        object payload, CancellationToken ct = default);
}
