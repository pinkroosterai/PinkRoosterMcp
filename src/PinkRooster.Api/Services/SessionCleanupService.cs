using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;

namespace PinkRooster.Api.Services;

public sealed class SessionCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<SessionCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredSessionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during session cleanup");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CleanupExpiredSessionsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;
        var deleted = await db.UserSessions
            .Where(s => s.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation("Cleaned up {Count} expired session(s)", deleted);
    }
}
