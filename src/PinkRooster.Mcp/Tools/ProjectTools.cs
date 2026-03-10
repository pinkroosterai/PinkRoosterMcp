using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Inputs;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class ProjectTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "get_project_status", ReadOnly = true,
        Title = "Get Project Status", OpenWorld = false)]
    [Description(
        "Call first when starting work on a project. " +
        "Resolves a project by its filesystem path and returns its ID (for use with all other tools) " +
        "plus a compact status summary with issue counts and work package breakdown. " +
        "Does not include individual entity details — use get_issue_details or get_work_package_details to drill in.")]
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

    [McpServerTool(Name = "get_next_actions", ReadOnly = true,
        Title = "Get Next Actions", OpenWorld = false)]
    [Description(
        "Returns a priority-ordered list of actionable work items (tasks, work packages, issues) " +
        "that need attention. Items are sorted by priority then entity type (tasks first). " +
        "Use after get_project_status to decide what to work on next. " +
        "Does not include blocked or terminal items.")]
    public async Task<string> GetNextActions(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Maximum number of items to return. Default 10.")] int limit = 10,
        [Description("Filter by entity type. Omit for all types.")] EntityTypeFilter? entityType = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectId(projectId, out var projId))
            return OperationResult.Error(
                $"Invalid project ID '{projectId}'. Expected format: 'proj-{{number}}'.");

        try
        {
            var entityTypeStr = entityType?.ToString().ToLowerInvariant();
            var items = await apiClient.GetNextActionsAsync(projId, limit, entityTypeStr, ct);

            if (items is null)
                return OperationResult.Error($"Project {projectId} not found.");

            return JsonSerializer.Serialize(items, JsonDefaults.Indented);
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to fetch next actions: {ex.Message}");
        }
    }

    [McpServerTool(Name = "create_or_update_project",
        Title = "Create or Update Project", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Creates or updates a project, matched by path. Returns the project ID. " +
        "Required to register a project before using other tools. " +
        "If the project already exists at this path, it updates name and description.")]
    public async Task<string> CreateOrUpdateProject(
        [Description("Project display name.")] string name,
        [Description("Short project description.")] string description,
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
