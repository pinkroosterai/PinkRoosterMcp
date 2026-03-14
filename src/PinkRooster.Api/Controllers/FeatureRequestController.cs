using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Extensions;
using PinkRooster.Api.Services;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:long}/feature-requests")]
public sealed class FeatureRequestController(IFeatureRequestService featureRequestService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FeatureRequestResponse>>> GetAll(
        long projectId, [FromQuery] string? state, CancellationToken ct)
    {
        var featureRequests = await featureRequestService.GetByProjectAsync(projectId, state, ct);
        return Ok(featureRequests);
    }

    [HttpGet("{frNumber:int}")]
    public async Task<ActionResult<FeatureRequestResponse>> GetByNumber(
        long projectId, int frNumber, CancellationToken ct)
    {
        var fr = await featureRequestService.GetByNumberAsync(projectId, frNumber, ct);
        return fr is null ? NotFound() : Ok(fr);
    }

    [HttpPost]
    public async Task<ActionResult<FeatureRequestResponse>> Create(
        long projectId, CreateFeatureRequestRequest request, CancellationToken ct)
    {
        var changedBy = HttpContext.GetCallerIdentity();
        var fr = await featureRequestService.CreateAsync(projectId, request, changedBy, ct);
        return Created($"api/projects/{projectId}/feature-requests/{fr.FeatureRequestNumber}", fr);
    }

    [HttpPatch("{frNumber:int}")]
    public async Task<ActionResult<FeatureRequestResponse>> Update(
        long projectId, int frNumber, UpdateFeatureRequestRequest request, CancellationToken ct)
    {
        var changedBy = HttpContext.GetCallerIdentity();
        var fr = await featureRequestService.UpdateAsync(projectId, frNumber, request, changedBy, ct);
        return fr is null ? NotFound() : Ok(fr);
    }

    [HttpPost("{frNumber:int}/user-stories/manage")]
    public async Task<ActionResult<FeatureRequestResponse>> ManageUserStories(
        long projectId, int frNumber, ManageUserStoriesRequest request, CancellationToken ct)
    {
        var changedBy = HttpContext.GetCallerIdentity();
        var fr = await featureRequestService.ManageUserStoriesAsync(projectId, frNumber, request, changedBy, ct);
        return fr is null ? NotFound() : Ok(fr);
    }

    [HttpDelete("{frNumber:int}")]
    public async Task<ActionResult> Delete(
        long projectId, int frNumber, CancellationToken ct)
    {
        var deleted = await featureRequestService.DeleteAsync(projectId, frNumber, ct);
        return deleted ? NoContent() : NotFound();
    }
}
