using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;

namespace PinkRooster.Api.Services;

public sealed class WebhookRetryService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookRetryService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private const int MaxRetries = 5;
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRetryBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing webhook retry batch");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessRetryBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;
        var pendingRetries = await db.WebhookDeliveryLogs
            .Include(l => l.Subscription)
            .Where(l => l.NextRetryAt != null && l.NextRetryAt <= now && l.Subscription.IsActive)
            .OrderBy(l => l.NextRetryAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pendingRetries.Count == 0) return;

        logger.LogInformation("Processing {Count} webhook retries", pendingRetries.Count);

        foreach (var log in pendingRetries)
        {
            var sw = Stopwatch.StartNew();
            int? statusCode = null;
            string? responseBody = null;
            bool success = false;

            try
            {
                var client = httpClientFactory.CreateClient("webhook");
                client.Timeout = RequestTimeout;

                var signature = ComputeHmacSha256(log.Payload, log.Subscription.Secret);

                var request = new HttpRequestMessage(HttpMethod.Post, log.Subscription.Url)
                {
                    Content = new StringContent(log.Payload, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-Webhook-Signature", $"sha256={signature}");
                request.Headers.Add("X-Webhook-Event", log.EventType);

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

            log.AttemptNumber++;
            log.HttpStatusCode = statusCode;
            log.ResponseBody = responseBody?.Length > 2000 ? responseBody[..2000] : responseBody;
            log.DurationMs = (int)sw.ElapsedMilliseconds;
            log.Success = success;

            if (success)
            {
                log.NextRetryAt = null;
                log.Subscription.LastDeliveredAt = DateTimeOffset.UtcNow;
                log.Subscription.ConsecutiveFailures = 0;
            }
            else
            {
                log.Subscription.LastFailedAt = DateTimeOffset.UtcNow;
                log.Subscription.ConsecutiveFailures++;

                if (log.AttemptNumber < MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, log.AttemptNumber) * 30);
                    log.NextRetryAt = DateTimeOffset.UtcNow.Add(delay);
                }
                else
                {
                    log.NextRetryAt = null; // terminal — no more retries
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexStringLower(hash);
    }
}
