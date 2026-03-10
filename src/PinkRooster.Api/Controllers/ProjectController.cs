using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Services;
using PinkRooster.Shared.Constants;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Projects.Route)]
public sealed class ProjectController(IProjectService projectService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get([FromQuery] string? path, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            var project = await projectService.GetByPathAsync(path, ct);
            return project is null ? NotFound() : Ok(project);
        }

        var projects = await projectService.GetAllAsync(ct);
        return Ok(projects);
    }

    [HttpPut]
    public async Task<ActionResult<ProjectResponse>> CreateOrUpdate(
        CreateOrUpdateProjectRequest request, CancellationToken ct)
    {
        var (project, isNew) = await projectService.CreateOrUpdateAsync(request, ct);
        return isNew ? Created($"{ApiRoutes.Projects.Route}?path={project.ProjectPath}", project) : Ok(project);
    }

    [HttpGet("{projectId:long}/status")]
    public async Task<ActionResult<ProjectStatusResponse>> GetStatus(long projectId, CancellationToken ct)
    {
        var status = await projectService.GetStatusAsync(projectId, ct);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id, CancellationToken ct)
    {
        var deleted = await projectService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
