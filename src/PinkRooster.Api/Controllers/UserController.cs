using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Extensions;
using PinkRooster.Api.Services;
using PinkRooster.Shared.Constants;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UserController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AuthUserResponse>>> GetAll(CancellationToken ct)
    {
        if (!IsSuperUserOrApiKey())
            return this.ProblemForbidden("Insufficient permissions");

        var users = await userService.GetAllAsync(ct);
        return Ok(users);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AuthUserResponse>> GetById(long id, CancellationToken ct)
    {
        // SuperUser, API key, or self can view
        if (!IsSuperUserOrApiKey() && !IsSelf(id))
            return this.ProblemForbidden("Insufficient permissions");

        var user = await userService.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<AuthUserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (!IsSuperUserOrApiKey())
            return this.ProblemForbidden("Insufficient permissions");

        try
        {
            var user = await userService.CreateAsync(
                request.Email, request.Password, request.DisplayName, GlobalRole.User, ct);
            return Created($"/api/users/{user.Id}", user);
        }
        catch (InvalidOperationException ex)
        {
            return this.ProblemConflict(ex.Message);
        }
    }

    [HttpPatch("{id:long}")]
    public async Task<ActionResult<AuthUserResponse>> Update(long id, UpdateUserRequest request, CancellationToken ct)
    {
        // SuperUser, API key, or self (limited to profile fields)
        if (!IsSuperUserOrApiKey() && !IsSelf(id))
            return this.ProblemForbidden("Insufficient permissions");

        // Non-SuperUser self-edit: only allow displayName changes (email change must go through /api/auth/me with password verification)
        GlobalRole? globalRole = null;
        bool? isActive = null;
        string? email = null;
        if (IsSuperUserOrApiKey())
        {
            globalRole = request.GlobalRole;
            isActive = request.IsActive;
            email = request.Email;
        }

        var user = await userService.UpdateAsync(id, request.DisplayName, email, globalRole, isActive, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Deactivate(long id, CancellationToken ct)
    {
        if (!IsSuperUserOrApiKey())
            return this.ProblemForbidden("Insufficient permissions");

        var deactivated = await userService.DeactivateAsync(id, ct);
        return deactivated ? NoContent() : NotFound();
    }

    // ── Helpers ──

    private bool IsApiKeyCaller()
    {
        return HttpContext.Items.ContainsKey(AuthConstants.CallerIdentityKey) &&
               !HttpContext.Items.ContainsKey(AuthConstants.UserIdKey);
    }

    private bool IsSuperUser()
    {
        // Check via a simple query — but we already have the user from session middleware
        // For now, we check the CallerIdentity pattern. A more robust approach would cache the user's role.
        if (!HttpContext.Items.TryGetValue(AuthConstants.UserIdKey, out var userIdObj) || userIdObj is not long)
            return false;

        // We need to check GlobalRole — use a simple approach via the auth pipeline
        // The ProjectAuthorizationMiddleware already exempts non-project routes, so we handle auth here
        return HttpContext.Items.TryGetValue(AuthConstants.UserGlobalRoleKey, out var roleObj) &&
               roleObj is string role && role == "SuperUser";
    }

    private bool IsSuperUserOrApiKey() => IsApiKeyCaller() || IsSuperUser();

    private bool IsSelf(long targetUserId)
    {
        return HttpContext.Items.TryGetValue(AuthConstants.UserIdKey, out var userIdObj) &&
               userIdObj is long userId && userId == targetUserId;
    }
}

// ── Request DTOs ──

public sealed class CreateUserRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string DisplayName { get; init; }
}

public sealed class UpdateUserRequest
{
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public GlobalRole? GlobalRole { get; init; }
    public bool? IsActive { get; init; }
}
