using System.Security.Cryptography;
using System.Text;
using PinkRooster.Shared.Constants;

namespace PinkRooster.Api.Middleware;

public sealed class ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly byte[][] _validKeyBytes = (configuration
        .GetSection("Auth:ApiKeys")
        .Get<string[]>() ?? [])
        .Select(k => Encoding.UTF8.GetBytes(k))
        .ToArray();

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
        if (!IsValidKeyConstantTime(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new { Error = "Invalid API key" });
        }

        // Store caller identity for downstream use (logging, etc.)
        context.Items[AuthConstants.CallerIdentityKey] = apiKey[..Math.Min(8, apiKey.Length)] + "...";

        return next(context);
    }

    private bool IsValidKeyConstantTime(string apiKey)
    {
        var inputBytes = Encoding.UTF8.GetBytes(apiKey);
        var match = false;

        foreach (var validKeyBytes in _validKeyBytes)
        {
            if (inputBytes.Length == validKeyBytes.Length &&
                CryptographicOperations.FixedTimeEquals(inputBytes, validKeyBytes))
            {
                match = true;
            }
        }

        return match;
    }
}
