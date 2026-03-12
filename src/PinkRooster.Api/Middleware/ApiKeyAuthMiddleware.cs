using System.Security.Cryptography;
using System.Text;
using PinkRooster.Shared.Constants;

namespace PinkRooster.Api.Middleware;

public sealed class ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyAuthMiddleware> logger)
{
    private readonly byte[][] _validKeyBytes = InitializeKeys(configuration, logger);

    private static byte[][] InitializeKeys(IConfiguration configuration, ILogger logger)
    {
        var keys = (configuration
            .GetSection("Auth:ApiKeys")
            .Get<string[]>() ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => Encoding.UTF8.GetBytes(k))
            .ToArray();

        if (keys.Length == 0)
            logger.LogInformation("No API keys configured — running in open access mode");
        else
            logger.LogInformation("API key authentication enabled with {KeyCount} key(s)", keys.Length);

        return keys;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Allow health endpoint without auth
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
            return next(context);

        // No keys configured — open access
        if (_validKeyBytes.Length == 0)
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
