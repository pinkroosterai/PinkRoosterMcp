using System.Text.RegularExpressions;
using PinkRooster.Api.Services;
using PinkRooster.Shared.Constants;

namespace PinkRooster.Api.Middleware;

public sealed partial class ProjectAuthorizationMiddleware(RequestDelegate next)
{
    // Match /api/projects/{projectId}/ or /api/projects/{projectId} (end of path)
    [GeneratedRegex(@"^/api/projects/(\d+)(?:/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectIdPattern();

    private static readonly string[] ExemptPrefixes =
    [
        "/health",
        "/api/auth/",
        "/api/activity-logs",
        "/api/events",
        "/api/users"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Exempt paths
        if (ExemptPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) ||
            path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // API key callers bypass RBAC (CallerIdentity set but no UserId)
        if (context.Items.ContainsKey(AuthConstants.CallerIdentityKey) && !context.Items.ContainsKey(AuthConstants.UserIdKey))
        {
            await next(context);
            return;
        }

        // Extract projectId from route
        var match = ProjectIdPattern().Match(path);
        if (!match.Success)
        {
            // Non-project routes (e.g., /api/projects list) — handled by controller
            await next(context);
            return;
        }

        var projectId = long.Parse(match.Groups[1].Value);

        if (!context.Items.TryGetValue(AuthConstants.UserIdKey, out var userIdObj) || userIdObj is not long userId)
        {
            // No authenticated session — unauthenticated requests to project routes are rejected
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Authentication required" });
            return;
        }

        var roleService = context.RequestServices.GetRequiredService<IProjectRoleService>();
        var effectiveRole = await roleService.GetEffectiveRoleAsync(userId, projectId);

        if (effectiveRole is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Insufficient permissions" });
            return;
        }

        // Check HTTP method against required role level
        var method = context.Request.Method;
        var hasPermission = method switch
        {
            "GET" or "HEAD" or "OPTIONS" => true, // Viewer+ (any role)
            "DELETE" => effectiveRole is "SuperUser" or "Admin",
            _ => effectiveRole is "SuperUser" or "Admin" or "Editor" // POST, PUT, PATCH
        };

        if (!hasPermission)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Insufficient permissions" });
            return;
        }

        await next(context);
    }
}
