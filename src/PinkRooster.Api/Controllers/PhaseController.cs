using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Services;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:long}/work-packages/{wpNumber:int}/phases")]
public sealed class PhaseController(IPhaseService phaseService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PhaseResponse>> Create(
        long projectId, int wpNumber, CreatePhaseRequest request, CancellationToken ct)
    {
        try
        {
            var changedBy = GetCallerIdentity();
            var phase = await phaseService.CreateAsync(projectId, wpNumber, request, changedBy, ct);
            return Created("", phase);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{phaseNumber:int}")]
    public async Task<ActionResult<PhaseResponse>> Update(
        long projectId, int wpNumber, int phaseNumber, UpdatePhaseRequest request, CancellationToken ct)
    {
        var changedBy = GetCallerIdentity();
        var phase = await phaseService.UpdateAsync(projectId, wpNumber, phaseNumber, request, changedBy, ct: ct);
        return phase is null ? NotFound() : Ok(phase);
    }

    [HttpDelete("{phaseNumber:int}")]
    public async Task<ActionResult> Delete(
        long projectId, int wpNumber, int phaseNumber, CancellationToken ct)
    {
        var deleted = await phaseService.DeleteAsync(projectId, wpNumber, phaseNumber, ct);
        return deleted ? NoContent() : NotFound();
    }

    private string GetCallerIdentity()
    {
        return HttpContext.Items["CallerIdentity"] as string ?? "unknown";
    }
}
