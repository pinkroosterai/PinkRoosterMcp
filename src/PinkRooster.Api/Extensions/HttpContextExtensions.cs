using PinkRooster.Shared.Constants;

namespace PinkRooster.Api.Extensions;

public static class HttpContextExtensions
{
    public static string GetCallerIdentity(this HttpContext httpContext)
    {
        return httpContext.Items[AuthConstants.CallerIdentityKey] as string ?? "unknown";
    }
}
