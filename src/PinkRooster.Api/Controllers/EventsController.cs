using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Services;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:long}/events")]
public sealed class EventsController(IEventBroadcaster broadcaster) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly TimeSpan MaxConnectionLifetime = TimeSpan.FromHours(1);

    [HttpGet]
    public async Task Stream(long projectId, CancellationToken ct)
    {
        if (!broadcaster.TryAcquireConnection(projectId))
        {
            Response.StatusCode = 429;
            Response.ContentType = "application/json";
            await Response.WriteAsync("""{"error":"Too many SSE connections for this project."}""", ct);
            return;
        }

        try
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";
            Response.Headers["X-Accel-Buffering"] = "no";

            await Response.Body.FlushAsync(ct);

            using var lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            lifetimeCts.CancelAfter(MaxConnectionLifetime);
            var linkedCt = lifetimeCts.Token;

            var heartbeatInterval = TimeSpan.FromSeconds(30);
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCt);
            var heartbeatTask = SendHeartbeatsAsync(heartbeatInterval, heartbeatCts.Token);

            try
            {
                await foreach (var evt in broadcaster.Subscribe(projectId, linkedCt))
                {
                    var data = JsonSerializer.Serialize(evt, JsonOptions);
                    await Response.WriteAsync($"event: {evt.EventType}\ndata: {data}\n\n", linkedCt);
                    await Response.Body.FlushAsync(linkedCt);
                }
            }
            finally
            {
                await heartbeatCts.CancelAsync();
                try { await heartbeatTask; } catch (OperationCanceledException) { }
            }
        }
        finally
        {
            broadcaster.ReleaseConnection(projectId);
        }
    }

    private async Task SendHeartbeatsAsync(TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await Response.WriteAsync(": heartbeat\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            catch (Exception)
            {
                break;
            }
        }
    }
}
