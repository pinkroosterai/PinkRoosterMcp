using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Extensions;
using PinkRooster.Api.Services;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:long}/memories")]
public sealed class ProjectMemoryController(IProjectMemoryService memoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProjectMemoryListItemResponse>>> GetAll(
        long projectId, [FromQuery] string? namePattern, [FromQuery] string? tag, CancellationToken ct)
    {
        var memories = await memoryService.GetByProjectAsync(projectId, namePattern, tag, ct);
        return Ok(memories);
    }

    [HttpGet("{memoryNumber:int}")]
    public async Task<ActionResult<ProjectMemoryResponse>> GetByNumber(
        long projectId, int memoryNumber, CancellationToken ct)
    {
        var memory = await memoryService.GetByNumberAsync(projectId, memoryNumber, ct);
        return memory is null ? NotFound() : Ok(memory);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectMemoryResponse>> Upsert(
        long projectId, UpsertProjectMemoryRequest request, CancellationToken ct)
    {
        var changedBy = HttpContext.GetCallerIdentity();
        var memory = await memoryService.UpsertAsync(projectId, request, changedBy, ct);
        var statusCode = memory.WasMerged ? 200 : 201;
        return StatusCode(statusCode, memory);
    }

    [HttpDelete("{memoryNumber:int}")]
    public async Task<ActionResult> Delete(
        long projectId, int memoryNumber, CancellationToken ct)
    {
        var deleted = await memoryService.DeleteAsync(projectId, memoryNumber, ct);
        return deleted ? NoContent() : NotFound();
    }
}
