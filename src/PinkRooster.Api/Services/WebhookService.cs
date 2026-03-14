using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;

namespace PinkRooster.Api.Services;

public sealed class WebhookService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookService> logger) : IWebhookService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const int MaxRetries = 5;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public async Task EnqueueDeliveryAsync(long projectId, string eventType, string entityType, string entityId,
        object payload, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var subscriptions = await db.WebhookSubscriptions
            .Where(s => s.ProjectId == projectId && s.IsActive)
            .ToListAsync(ct);

        if (subscriptions.Count == 0) return;

        var payloadJson = JsonSerializer.Serialize(new
        {
            eventType,
            entityType,
            entityId,
            timestamp = DateTimeOffset.UtcNow,
            data = payload
        }, JsonOptions);

        foreach (var sub in subscriptions)
        {
            if (!MatchesFilter(sub.EventFilters, eventType, entityType))
                continue;

            await DeliverAsync(db, sub, eventType, entityType, entityId, payloadJson, ct);
        }
    }

    private async Task DeliverAsync(AppDbContext db, WebhookSubscription subscription,
        string eventType, string entityType, string entityId, string payload, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        int? statusCode = null;
        string? responseBody = null;
        bool success = false;

        try
        {
            var client = httpClientFactory.CreateClient("webhook");
            client.Timeout = RequestTimeout;

            var signature = ComputeHmacSha256(payload, subscription.Secret);

            var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Webhook-Signature", $"sha256={signature}");
            request.Headers.Add("X-Webhook-Event", eventType);

            var response = await client.SendAsync(request, ct);
            statusCode = (int)response.StatusCode;
            responseBody = await response.Content.ReadAsStringAsync(ct);
            success = response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            responseBody = "Request timed out";
        }
        catch (HttpRequestException ex)
        {
            responseBody = ex.Message;
        }
        catch (Exception ex)
        {
            responseBody = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            sw.Stop();
        }

        var log = new WebhookDeliveryLog
        {
            WebhookSubscriptionId = subscription.Id,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            AttemptNumber = 1,
            Payload = payload,
            HttpStatusCode = statusCode,
            ResponseBody = responseBody?.Length > 2000 ? responseBody[..2000] : responseBody,
            DurationMs = (int)sw.ElapsedMilliseconds,
            Success = success
        };

        if (success)
        {
            subscription.LastDeliveredAt = DateTimeOffset.UtcNow;
            subscription.ConsecutiveFailures = 0;
        }
        else
        {
            subscription.LastFailedAt = DateTimeOffset.UtcNow;
            subscription.ConsecutiveFailures++;

            if (subscription.ConsecutiveFailures < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, subscription.ConsecutiveFailures) * 30);
                log.NextRetryAt = DateTimeOffset.UtcNow.Add(delay);
            }
        }

        db.WebhookDeliveryLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }

    private static bool MatchesFilter(List<WebhookEventFilter> filters, string eventType, string entityType)
    {
        if (filters.Count == 0) return true;

        return filters.Any(f =>
            f.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase) &&
            (f.EntityType is null || f.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase)));
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexStringLower(hash);
    }
}
