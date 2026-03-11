using System.Diagnostics;
using System.Text.RegularExpressions;
using PinkRooster.Api.Services;
using PinkRooster.Shared.Constants;
using PinkRooster.Shared.DTOs;

namespace PinkRooster.Api.Middleware;

public sealed partial class RequestLoggingMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/api/activity-logs"
    };

    [GeneratedRegex(@"^/api/projects/(\d+)/", RegexOptions.Compiled)]
    private static partial Regex ProjectIdPattern();

    public async Task InvokeAsync(HttpContext context, IActivityLogService activityLogService, IEventBroadcaster broadcaster)
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
            if (!ExcludedPaths.Contains(path) && !path.EndsWith("/events", StringComparison.OrdinalIgnoreCase))
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

                // Publish activity event for dashboard live updates
                var match = ProjectIdPattern().Match(path);
                if (match.Success && long.TryParse(match.Groups[1].Value, out var projectId))
                {
                    broadcaster.Publish(new ServerEvent
                    {
                        EventType = "activity:logged",
                        EntityType = "ActivityLog",
                        EntityId = path,
                        Action = context.Request.Method,
                        ProjectId = projectId
                    });
                }
            }
        }
    }
}
