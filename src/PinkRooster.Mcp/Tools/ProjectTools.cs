using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class ProjectTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "get_project_status", ReadOnly = true)]
    [Description(
        "Call first when starting work on a project. " +
        "Returns project ID and a compact status summary with issue counts and work package breakdown.")]
    public async Task<string> GetProjectStatus(
        [Description("Absolute path to the project root directory.")] string projectPath,
        CancellationToken ct = default)
    {
        var project = await apiClient.GetProjectByPathAsync(projectPath, ct);

        if (project is null)
            return OperationResult.Warning(
                $"No project found at '{projectPath}'. " +
                "Call create_or_update_project to register it.");

        if (!IdParser.TryParseProjectId(project.ProjectId, out var projId))
            return OperationResult.Error($"Failed to parse project ID '{project.ProjectId}'.");

        try
        {
            var status = await apiClient.GetProjectStatusAsync(projId, ct);

            if (status is null)
                return OperationResult.Error($"Project {project.ProjectId} not found.");

            return JsonSerializer.Serialize(status, JsonDefaults.Indented);
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to fetch project status: {ex.Message}");
        }
    }

    [McpServerTool(Name = "create_or_update_project")]
    [Description("Creates or updates a project, matched by path. Returns the project id.")]
    public async Task<string> CreateOrUpdateProject(
        [Description("Display name.")] string name,
        [Description("Short description.")] string description,
        [Description("Absolute path to the project root directory.")] string projectPath,
        CancellationToken ct = default)
    {
        var request = new CreateOrUpdateProjectRequest
        {
            Name = name,
            Description = description,
            ProjectPath = projectPath
        };

        var (project, isNew) = await apiClient.CreateOrUpdateProjectAsync(request, ct);

        return isNew
            ? OperationResult.Success(project.ProjectId, $"Project '{name}' created.")
            : OperationResult.Success(project.ProjectId, $"Project '{name}' updated.");
    }
}
