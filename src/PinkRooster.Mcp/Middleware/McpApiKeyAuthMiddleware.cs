using PinkRooster.Shared.Constants;

namespace PinkRooster.Mcp.Middleware;

public sealed class McpApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly HashSet<string> _validKeys = configuration
        .GetSection("Auth:ApiKeys")
        .Get<string[]>()
        ?.Where(k => !string.IsNullOrWhiteSpace(k))
        .ToHashSet() ?? [];

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Allow health endpoint without auth
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
            return next(context);

        // No keys configured — open access
        if (_validKeys.Count == 0)
            return next(context);

        if (!context.Request.Headers.TryGetValue(AuthConstants.ApiKeyHeaderName, out var apiKeyHeader))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new { Error = "Missing API key" });
        }

        var apiKey = apiKeyHeader.ToString();
        if (!_validKeys.Contains(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new { Error = "Invalid API key" });
        }

        // Store caller identity for downstream use
        context.Items[AuthConstants.CallerIdentityKey] = apiKey[..Math.Min(8, apiKey.Length)] + "...";

        return next(context);
    }
}
