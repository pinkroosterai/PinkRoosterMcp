using System.Diagnostics;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.Constants;

namespace PinkRooster.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/api/activity-logs"
    };

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();

            var path = context.Request.Path.Value ?? "";
            if (!ExcludedPaths.Contains(path))
            {
                var callerIdentity = context.Items.TryGetValue(AuthConstants.CallerIdentityKey, out var identity)
                    ? identity as string
                    : null;

                db.ActivityLogs.Add(new ActivityLog
                {
                    HttpMethod = context.Request.Method,
                    Path = path,
                    StatusCode = context.Response.StatusCode,
                    DurationMs = sw.ElapsedMilliseconds,
                    CallerIdentity = callerIdentity,
                    Timestamp = DateTimeOffset.UtcNow
                });

                await db.SaveChangesAsync();
            }
        }
    }
}
