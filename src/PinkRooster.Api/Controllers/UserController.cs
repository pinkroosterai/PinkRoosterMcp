using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Services;
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
            return StatusCode(403, new { error = "Insufficient permissions" });

        var users = await userService.GetAllAsync(ct);
        return Ok(users);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AuthUserResponse>> GetById(long id, CancellationToken ct)
    {
        // SuperUser, API key, or self can view
        if (!IsSuperUserOrApiKey() && !IsSelf(id))
            return StatusCode(403, new { error = "Insufficient permissions" });

        var user = await userService.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<AuthUserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (!IsSuperUserOrApiKey())
            return StatusCode(403, new { error = "Insufficient permissions" });

        try
        {
            var user = await userService.CreateAsync(
                request.Email, request.Password, request.DisplayName, GlobalRole.User, ct);
            return Created($"/api/users/{user.Id}", user);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:long}")]
    public async Task<ActionResult<AuthUserResponse>> Update(long id, UpdateUserRequest request, CancellationToken ct)
    {
        // SuperUser, API key, or self (limited to profile fields)
        if (!IsSuperUserOrApiKey() && !IsSelf(id))
            return StatusCode(403, new { error = "Insufficient permissions" });

        // Non-SuperUser self-edit: only allow displayName and email changes
        GlobalRole? globalRole = null;
        bool? isActive = null;
        if (IsSuperUserOrApiKey())
        {
            globalRole = request.GlobalRole;
            isActive = request.IsActive;
        }

        var user = await userService.UpdateAsync(id, request.DisplayName, request.Email, globalRole, isActive, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Deactivate(long id, CancellationToken ct)
    {
        if (!IsSuperUserOrApiKey())
            return StatusCode(403, new { error = "Insufficient permissions" });

        var deactivated = await userService.DeactivateAsync(id, ct);
        return deactivated ? NoContent() : NotFound();
    }

    // ── Helpers ──

    private bool IsApiKeyCaller()
    {
        return HttpContext.Items.ContainsKey("CallerIdentity") &&
               !HttpContext.Items.ContainsKey("UserId");
    }

    private bool IsSuperUser()
    {
        // Check via a simple query — but we already have the user from session middleware
        // For now, we check the CallerIdentity pattern. A more robust approach would cache the user's role.
        if (!HttpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not long)
            return false;

        // We need to check GlobalRole — use a simple approach via the auth pipeline
        // The ProjectAuthorizationMiddleware already exempts non-project routes, so we handle auth here
        return HttpContext.Items.TryGetValue("UserGlobalRole", out var roleObj) &&
               roleObj is string role && role == "SuperUser";
    }

    private bool IsSuperUserOrApiKey() => IsApiKeyCaller() || IsSuperUser();

    private bool IsSelf(long targetUserId)
    {
        return HttpContext.Items.TryGetValue("UserId", out var userIdObj) &&
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
