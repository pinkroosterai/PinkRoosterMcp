using PinkRooster.Api.Services;
using PinkRooster.Shared.Constants;

namespace PinkRooster.Api.Middleware;

public sealed class SessionAuthMiddleware(RequestDelegate next, IWebHostEnvironment env)
{
    private const string CookieName = "pinkrooster_session";

    private static readonly string[] ExemptPaths =
    [
        "/health",
        "/api/auth/config",
        "/api/auth/register",
        "/api/auth/login"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Exempt paths don't need session validation
        if (ExemptPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        // If caller identity is already set (by an outer middleware), skip
        if (context.Items.ContainsKey(AuthConstants.CallerIdentityKey))
        {
            await next(context);
            return;
        }

        // Check for session cookie
        if (context.Request.Cookies.TryGetValue(CookieName, out var token) && !string.IsNullOrEmpty(token))
        {
            var authService = context.RequestServices.GetRequiredService<IAuthService>();
            var user = await authService.ValidateSessionAsync(token);

            if (user is not null)
            {
                // Valid session — set caller identity, user ID, and global role
                context.Items[AuthConstants.CallerIdentityKey] = user.Email;
                context.Items[AuthConstants.UserIdKey] = user.Id;
                context.Items[AuthConstants.UserGlobalRoleKey] = user.GlobalRole.ToString();
            }
            else
            {
                // Invalid/expired session — clear the cookie and fall through to API key auth
                context.Response.Cookies.Delete(CookieName, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = !env.IsDevelopment() && !env.IsEnvironment("Testing"),
                    SameSite = SameSiteMode.Strict,
                    Path = "/"
                });
            }
        }

        // Fall through to next middleware (API key auth will handle if no identity set)
        await next(context);
    }
}
