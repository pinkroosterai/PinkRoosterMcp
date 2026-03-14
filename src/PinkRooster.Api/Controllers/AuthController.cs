using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PinkRooster.Api.Extensions;
using PinkRooster.Api.Services;
using PinkRooster.Shared.Constants;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IProjectRoleService projectRoleService,
    IUserService userService) : ControllerBase
{
    private const string CookieName = "pinkrooster_session";

    private CookieOptions SessionCookieOptions => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/"
    };

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
    {
        var hasUsers = await authService.HasAnyUsersAsync(ct);
        return Ok(new { isProtected = hasUsers });
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth-register")]
    public async Task<ActionResult<AuthUserResponse>> Register(
        RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var user = await authService.RegisterAsync(request, ct);
            return Created($"/api/auth/me", user);
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemConflict(ex.Message);
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);

        if (result is null)
            return this.ProblemUnauthorized("Invalid email or password.");

        var (response, sessionToken) = result.Value;

        var options = SessionCookieOptions;
        options.MaxAge = TimeSpan.FromHours(24);
        Response.Cookies.Append(CookieName, sessionToken, options);

        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var token = Request.Cookies[CookieName];
        if (token is not null)
        {
            await authService.LogoutAsync(token, ct);
            Response.Cookies.Delete(CookieName, SessionCookieOptions);
        }

        return NoContent();
    }

    [HttpGet("me/permissions")]
    public async Task<ActionResult<UserPermissionsResponse>> GetPermissions(
        [FromQuery] long projectId, CancellationToken ct)
    {
        if (!HttpContext.Items.TryGetValue(AuthConstants.UserIdKey, out var userIdObj) || userIdObj is not long userId)
            return this.ProblemUnauthorized("Not authenticated.");

        var permissions = await projectRoleService.GetUserPermissionsAsync(userId, projectId, ct);
        return Ok(permissions);
    }

    [HttpPatch("me")]
    public async Task<ActionResult<AuthUserResponse>> UpdateProfile(
        UpdateProfileRequest request, CancellationToken ct)
    {
        if (!HttpContext.Items.TryGetValue(AuthConstants.UserIdKey, out var userIdObj) || userIdObj is not long userId)
            return this.ProblemUnauthorized("Not authenticated.");

        // Require current password when changing email
        if (request.Email is not null)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword))
                return this.ProblemBadRequest("Current password is required to change email.");

            if (!await userService.VerifyPasswordAsync(userId, request.CurrentPassword, ct))
                return this.ProblemBadRequest("Current password is incorrect.");
        }

        var user = await userService.UpdateProfileAsync(userId, request.DisplayName, request.Email, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("me/password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request, CancellationToken ct)
    {
        if (!HttpContext.Items.TryGetValue(AuthConstants.UserIdKey, out var userIdObj) || userIdObj is not long userId)
            return this.ProblemUnauthorized("Not authenticated.");

        var changed = await userService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
        if (!changed)
            return this.ProblemBadRequest("Current password is incorrect.");

        return Ok(new { message = "Password changed successfully." });
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthUserResponse>> Me(CancellationToken ct)
    {
        var token = Request.Cookies[CookieName];
        if (token is null)
            return this.ProblemUnauthorized("Not authenticated.");

        var user = await authService.GetCurrentUserAsync(token, ct);
        if (user is null)
            return this.ProblemUnauthorized("Session expired.");

        return Ok(user);
    }
}
