using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Helpers;
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
        "CALL THIS FIRST — resolves a project by filesystem path and returns projectId " +
        "(required by all other tools). Also returns a compact status summary: " +
        "issue/FR/WP counts by state, active items, blocked items, and priority overview. " +
        "Does NOT include individual entity details, audit history, or phase/task trees — " +
        "use get_issue_details, get_feature_request_details, or get_work_package_details to drill in.")]
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

        return await ToolErrorHandler.ExecuteAsync(async () =>
        {
            var status = await apiClient.GetProjectStatusAsync(projId, ct);

            if (status is null)
                return OperationResult.Error($"Project {project.ProjectId} not found.");

            return JsonSerializer.Serialize(status, JsonDefaults.Indented);
        }, "get project status");
    }

    [McpServerTool(Name = "get_next_actions", ReadOnly = true,
        Title = "Get Next Actions", OpenWorld = false)]
    [Description(
        "Returns a priority-ordered list of actionable work items (tasks, WPs, issues, feature requests) " +
        "that need attention. Sorted by priority, then entity type (tasks first, then WPs, issues, FRs). " +
        "Each item includes enriched context: WP/task items show linkedIssueName, linkedFrName, " +
        "workPackageType, estimatedComplexity; issues show issueType, severity; FRs show category. " +
        "Excludes blocked and terminal items. Does NOT return full entity details — " +
        "use the entity-specific detail tools to get complete data. " +
        "Use after get_project_status to decide what to work on next.")]
    public async Task<string> GetNextActions(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Maximum number of items to return (e.g. 5, 10, 20). Default: 10.")] int limit = 10,
        [Description("Filter by entity type. Omit for all types.")] EntityTypeFilter? entityType = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectId(projectId, out var projId))
            return OperationResult.Error(
                $"Invalid project ID '{projectId}'. Expected format: 'proj-{{number}}'.");

        return await ToolErrorHandler.ExecuteAsync(async () =>
        {
            var entityTypeStr = entityType?.ToString().ToLowerInvariant();
            var items = await apiClient.GetNextActionsAsync(projId, limit, entityTypeStr, ct);

            if (items is null)
                return OperationResult.Error($"Project {projectId} not found.");

            return JsonSerializer.Serialize(items, JsonDefaults.Indented);
        }, "get next actions");
    }

    [McpServerTool(Name = "create_or_update_project",
        Title = "Create or Update Project", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Creates or updates a project, matched by path. Returns OperationResult with the project ID. " +
        "Required to register a project before using other tools. " +
        "If the project already exists at this path, it updates name and description. " +
        "Does NOT create issues, work packages, or other entities — use entity-specific tools for that.")]
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
