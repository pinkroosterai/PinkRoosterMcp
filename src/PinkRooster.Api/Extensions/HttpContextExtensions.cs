namespace PinkRooster.Api.Extensions;

public static class HttpContextExtensions
{
    public static string GetCallerIdentity(this HttpContext httpContext)
    {
        return httpContext.Items["CallerIdentity"] as string ?? "unknown";
    }
}
