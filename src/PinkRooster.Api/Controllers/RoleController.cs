using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Services;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:long}/roles")]
public sealed class RoleController(IProjectRoleService projectRoleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserProjectRoleResponse>>> GetProjectRoles(
        long projectId, CancellationToken ct)
    {
        // Only SuperUser and Admin can view roles
        if (!await HasRoleManagementAccess(projectId, ct))
            return StatusCode(403, new { error = "Insufficient permissions" });

        var roles = await projectRoleService.GetProjectRolesAsync(projectId, ct);
        return Ok(roles);
    }

    [HttpPut("{userId:long}")]
    public async Task<ActionResult<UserProjectRoleResponse>> AssignRole(
        long projectId, long userId, AssignRoleRequest request, CancellationToken ct)
    {
        // API key callers can assign any role
        if (!IsApiKeyCaller())
        {
            if (!HttpContext.Items.TryGetValue("UserId", out var callerIdObj) || callerIdObj is not long callerId)
                return Unauthorized();

            var callerRole = await projectRoleService.GetEffectiveRoleAsync(callerId, projectId, ct);

            // Only SuperUser and Admin can assign roles
            if (callerRole is not ("SuperUser" or "Admin"))
                return StatusCode(403, new { error = "Insufficient permissions" });

            // Admin cannot assign Admin role — only SuperUser can
            if (callerRole == "Admin" && request.Role == ProjectRole.Admin)
                return StatusCode(403, new { error = "Only SuperUser can assign the Admin role." });
        }

        var result = await projectRoleService.AssignRoleAsync(userId, projectId, request.Role, ct);
        return Ok(result);
    }

    [HttpDelete("{userId:long}")]
    public async Task<IActionResult> RemoveRole(
        long projectId, long userId, CancellationToken ct)
    {
        if (!await HasRoleManagementAccess(projectId, ct))
            return StatusCode(403, new { error = "Insufficient permissions" });

        var removed = await projectRoleService.RemoveRoleAsync(userId, projectId, ct);
        return removed ? NoContent() : NotFound();
    }

    private bool IsApiKeyCaller()
    {
        // API key callers have CallerIdentity set but no UserId
        return HttpContext.Items.ContainsKey("CallerIdentity") &&
               !HttpContext.Items.ContainsKey("UserId");
    }

    private async Task<bool> HasRoleManagementAccess(long projectId, CancellationToken ct)
    {
        // API key callers (MCP/programmatic) bypass role checks
        if (IsApiKeyCaller())
            return true;

        if (!HttpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not long userId)
            return false;

        var role = await projectRoleService.GetEffectiveRoleAsync(userId, projectId, ct);
        return role is "SuperUser" or "Admin";
    }
}
