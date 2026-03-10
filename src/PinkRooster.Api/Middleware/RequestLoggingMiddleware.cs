using System.Diagnostics;
using PinkRooster.Api.Services;
using PinkRooster.Shared.Constants;

namespace PinkRooster.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/api/activity-logs"
    };

    public async Task InvokeAsync(HttpContext context, IActivityLogService activityLogService)
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

                await activityLogService.LogRequestAsync(
                    context.Request.Method,
                    path,
                    context.Response.StatusCode,
                    sw.ElapsedMilliseconds,
                    callerIdentity);
            }
        }
    }
}
