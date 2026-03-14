using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IWebhookSubscriptionService
{
    Task<List<WebhookSubscriptionResponse>> GetByProjectAsync(long projectId, CancellationToken ct = default);
    Task<WebhookSubscriptionResponse?> GetByIdAsync(long projectId, long subscriptionId, CancellationToken ct = default);
    Task<WebhookSubscriptionResponse> CreateAsync(long projectId, CreateWebhookSubscriptionRequest request, CancellationToken ct = default);
    Task<WebhookSubscriptionResponse?> UpdateAsync(long projectId, long subscriptionId, UpdateWebhookSubscriptionRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(long projectId, long subscriptionId, CancellationToken ct = default);
    Task<List<WebhookDeliveryLogResponse>> GetDeliveryLogsAsync(long projectId, long subscriptionId, int limit = 50, CancellationToken ct = default);
}
