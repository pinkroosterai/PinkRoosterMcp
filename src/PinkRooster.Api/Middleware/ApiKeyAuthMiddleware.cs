using PinkRooster.Shared.Constants;

namespace PinkRooster.Api.Middleware;

public sealed class ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly HashSet<string> _validKeys = configuration
        .GetSection("Auth:ApiKeys")
        .Get<string[]>()
        ?.ToHashSet() ?? [];

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Allow health endpoint without auth
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
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

        // Store caller identity for downstream use (logging, etc.)
        context.Items[AuthConstants.CallerIdentityKey] = apiKey[..Math.Min(8, apiKey.Length)] + "...";

        return next(context);
    }
}
