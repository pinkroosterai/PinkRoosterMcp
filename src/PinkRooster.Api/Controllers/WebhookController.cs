using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Services;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:long}/webhooks")]
public sealed class WebhookController(IWebhookSubscriptionService webhookService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WebhookSubscriptionResponse>>> GetAll(
        long projectId, CancellationToken ct)
    {
        var subs = await webhookService.GetByProjectAsync(projectId, ct);
        return Ok(subs);
    }

    [HttpGet("{subscriptionId:long}")]
    public async Task<ActionResult<WebhookSubscriptionResponse>> GetById(
        long projectId, long subscriptionId, CancellationToken ct)
    {
        var sub = await webhookService.GetByIdAsync(projectId, subscriptionId, ct);
        return sub is null ? NotFound() : Ok(sub);
    }

    [HttpPost]
    public async Task<ActionResult<WebhookSubscriptionResponse>> Create(
        long projectId, CreateWebhookSubscriptionRequest request, CancellationToken ct)
    {
        var sub = await webhookService.CreateAsync(projectId, request, ct);
        return Created($"api/projects/{projectId}/webhooks/{sub.Id}", sub);
    }

    [HttpPatch("{subscriptionId:long}")]
    public async Task<ActionResult<WebhookSubscriptionResponse>> Update(
        long projectId, long subscriptionId, UpdateWebhookSubscriptionRequest request, CancellationToken ct)
    {
        var sub = await webhookService.UpdateAsync(projectId, subscriptionId, request, ct);
        return sub is null ? NotFound() : Ok(sub);
    }

    [HttpDelete("{subscriptionId:long}")]
    public async Task<ActionResult> Delete(
        long projectId, long subscriptionId, CancellationToken ct)
    {
        var deleted = await webhookService.DeleteAsync(projectId, subscriptionId, ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{subscriptionId:long}/deliveries")]
    public async Task<ActionResult<List<WebhookDeliveryLogResponse>>> GetDeliveryLogs(
        long projectId, long subscriptionId, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var logs = await webhookService.GetDeliveryLogsAsync(projectId, subscriptionId, limit, ct);
        return Ok(logs);
    }
}
