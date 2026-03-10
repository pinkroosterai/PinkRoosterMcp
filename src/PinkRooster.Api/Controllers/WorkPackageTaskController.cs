using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Extensions;
using PinkRooster.Api.Services;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:long}/work-packages/{wpNumber:int}/tasks")]
public sealed class WorkPackageTaskController(IWorkPackageTaskService taskService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TaskResponse>> Create(
        long projectId, int wpNumber, [FromQuery] int phaseNumber, CreateTaskRequest request, CancellationToken ct)
    {
        try
        {
            var changedBy = HttpContext.GetCallerIdentity();
            var task = await taskService.CreateAsync(projectId, wpNumber, phaseNumber, request, changedBy, ct);
            return Created("", task);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("batch-states")]
    public async Task<ActionResult<BatchUpdateTaskStatesResponse>> BatchUpdateStates(
        long projectId, int wpNumber, BatchUpdateTaskStatesRequest request, CancellationToken ct)
    {
        var changedBy = HttpContext.GetCallerIdentity();
        var result = await taskService.BatchUpdateStatesAsync(projectId, wpNumber, request, changedBy, ct: ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{taskNumber:int}")]
    public async Task<ActionResult<TaskResponse>> Update(
        long projectId, int wpNumber, int taskNumber, UpdateTaskRequest request, CancellationToken ct)
    {
        var changedBy = HttpContext.GetCallerIdentity();
        var task = await taskService.UpdateAsync(projectId, wpNumber, taskNumber, request, changedBy, ct: ct);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpDelete("{taskNumber:int}")]
    public async Task<ActionResult> Delete(
        long projectId, int wpNumber, int taskNumber, CancellationToken ct)
    {
        var deleted = await taskService.DeleteAsync(projectId, wpNumber, taskNumber, ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{taskNumber:int}/dependencies")]
    public async Task<ActionResult<TaskDependencyResponse>> AddDependency(
        long projectId, int wpNumber, int taskNumber, ManageDependencyRequest request, CancellationToken ct)
    {
        try
        {
            var dep = await taskService.AddDependencyAsync(projectId, wpNumber, taskNumber, request, ct: ct);
            return Created("", dep);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{taskNumber:int}/dependencies/{dependsOnTaskId:long}")]
    public async Task<ActionResult> RemoveDependency(
        long projectId, int wpNumber, int taskNumber, long dependsOnTaskId, CancellationToken ct)
    {
        var removed = await taskService.RemoveDependencyAsync(projectId, wpNumber, taskNumber, dependsOnTaskId, ct: ct);
        return removed ? NoContent() : NotFound();
    }

}
