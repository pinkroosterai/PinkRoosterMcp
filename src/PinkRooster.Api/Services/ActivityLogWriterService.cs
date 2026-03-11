using PinkRooster.Data;
using PinkRooster.Data.Entities;

namespace PinkRooster.Api.Services;

public sealed class ActivityLogWriterService(
    ActivityLogChannel channel,
    IServiceScopeFactory scopeFactory,
    ILogger<ActivityLogWriterService> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<ActivityLog>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for at least one entry
                if (await channel.Reader.WaitToReadAsync(stoppingToken))
                {
                    // Drain up to BatchSize entries
                    while (batch.Count < BatchSize && channel.Reader.TryRead(out var entry))
                    {
                        batch.Add(new ActivityLog
                        {
                            HttpMethod = entry.HttpMethod,
                            Path = entry.Path,
                            StatusCode = entry.StatusCode,
                            DurationMs = entry.DurationMs,
                            CallerIdentity = entry.CallerIdentity,
                            Timestamp = entry.Timestamp
                        });
                    }

                    if (batch.Count > 0)
                    {
                        await FlushBatchAsync(batch, stoppingToken);
                        batch.Clear();
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down — flush remaining entries
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error writing activity log batch");
                batch.Clear();
                await Task.Delay(FlushInterval, stoppingToken);
            }
        }

        // Drain remaining on shutdown
        while (channel.Reader.TryRead(out var entry))
        {
            batch.Add(new ActivityLog
            {
                HttpMethod = entry.HttpMethod,
                Path = entry.Path,
                StatusCode = entry.StatusCode,
                DurationMs = entry.DurationMs,
                CallerIdentity = entry.CallerIdentity,
                Timestamp = entry.Timestamp
            });
        }

        if (batch.Count > 0)
            await FlushBatchAsync(batch, CancellationToken.None);
    }

    private async Task FlushBatchAsync(List<ActivityLog> batch, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ActivityLogs.AddRange(batch);
        await db.SaveChangesAsync(ct);
    }
}
