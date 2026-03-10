using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Services;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:long}/work-packages")]
public sealed class WorkPackageController(IWorkPackageService workPackageService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WorkPackageResponse>>> GetAll(
        long projectId, [FromQuery] string? state, CancellationToken ct)
    {
        var wps = await workPackageService.GetByProjectAsync(projectId, state, ct);
        return Ok(wps);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<WorkPackageSummaryResponse>> GetSummary(
        long projectId, CancellationToken ct)
    {
        var summary = await workPackageService.GetSummaryAsync(projectId, ct);
        return Ok(summary);
    }

    [HttpGet("{wpNumber:int}")]
    public async Task<ActionResult<WorkPackageResponse>> GetByNumber(
        long projectId, int wpNumber, CancellationToken ct)
    {
        var wp = await workPackageService.GetByNumberAsync(projectId, wpNumber, ct);
        return wp is null ? NotFound() : Ok(wp);
    }

    [HttpPost]
    public async Task<ActionResult<WorkPackageResponse>> Create(
        long projectId, CreateWorkPackageRequest request, CancellationToken ct)
    {
        var changedBy = GetCallerIdentity();
        var wp = await workPackageService.CreateAsync(projectId, request, changedBy, ct);
        return Created($"api/projects/{projectId}/work-packages/{wp.WorkPackageNumber}", wp);
    }

    [HttpPatch("{wpNumber:int}")]
    public async Task<ActionResult<WorkPackageResponse>> Update(
        long projectId, int wpNumber, UpdateWorkPackageRequest request, CancellationToken ct)
    {
        var changedBy = GetCallerIdentity();
        var wp = await workPackageService.UpdateAsync(projectId, wpNumber, request, changedBy, ct: ct);
        return wp is null ? NotFound() : Ok(wp);
    }

    [HttpDelete("{wpNumber:int}")]
    public async Task<ActionResult> Delete(
        long projectId, int wpNumber, CancellationToken ct)
    {
        var deleted = await workPackageService.DeleteAsync(projectId, wpNumber, ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{wpNumber:int}/dependencies")]
    public async Task<ActionResult<DependencyResponse>> AddDependency(
        long projectId, int wpNumber, ManageDependencyRequest request, CancellationToken ct)
    {
        try
        {
            var dep = await workPackageService.AddDependencyAsync(projectId, wpNumber, request, ct: ct);
            return Created("", dep);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{wpNumber:int}/dependencies/{dependsOnWpId:long}")]
    public async Task<ActionResult> RemoveDependency(
        long projectId, int wpNumber, long dependsOnWpId, CancellationToken ct)
    {
        var removed = await workPackageService.RemoveDependencyAsync(projectId, wpNumber, dependsOnWpId, ct: ct);
        return removed ? NoContent() : NotFound();
    }

    private string GetCallerIdentity()
    {
        return HttpContext.Items["CallerIdentity"] as string ?? "unknown";
    }
}
