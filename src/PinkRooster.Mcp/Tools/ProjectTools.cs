using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Helpers;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class ProjectTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "get_project_overview", ReadOnly = true)]
    [Description(
        "Call first when starting work on a project. " +
        "Returns an overview of the project including the project id.")]
    public async Task<string> GetProjectOverview(
        [Description("Absolute path to the project root directory.")] string projectPath,
        CancellationToken ct = default)
    {
        var project = await apiClient.GetProjectByPathAsync(projectPath, ct);

        if (project is null)
            return OperationResult.Warning(
                $"No project found at '{projectPath}'. " +
                "Call create_or_update_project to register it.");

        var overview = new ProjectOverviewResponse
        {
            ProjectId = project.ProjectId,
            Name = project.Name,
            Description = project.Description,
            ProjectPath = project.ProjectPath,
            Status = project.Status
        };

        // Enrich with issue summaries
        if (IdParser.TryParseProjectId(project.ProjectId, out var projId))
        {
            try
            {
                IssueOverviewItem ToOverviewItem(Shared.DTOs.Responses.IssueResponse i) => new()
                {
                    IssueId = i.IssueId,
                    Name = i.Name,
                    State = i.State,
                    Priority = i.Priority,
                    Severity = i.Severity,
                    IssueType = i.IssueType,
                    CreatedAt = i.CreatedAt,
                    ResolvedAt = i.ResolvedAt
                };

                var active = await apiClient.GetIssuesByProjectAsync(projId, "active", ct);
                overview.ActiveIssues = active.Select(ToOverviewItem).ToList();

                var inactive = await apiClient.GetIssuesByProjectAsync(projId, "inactive", ct);
                overview.InactiveIssues = inactive.Select(ToOverviewItem).ToList();

                var summary = await apiClient.GetIssueSummaryAsync(projId, ct);
                overview.LatestTerminalIssues = summary.LatestTerminalIssues.Select(ToOverviewItem).ToList();
            }
            catch
            {
                // Issue data is non-critical; proceed with project overview only
            }

            // Enrich with work package summaries
            try
            {
                WorkPackageOverviewItem ToWpItem(Shared.DTOs.Responses.WorkPackageResponse wp) => new()
                {
                    WorkPackageId = wp.WorkPackageId,
                    Name = wp.Name,
                    Type = wp.Type,
                    Priority = wp.Priority,
                    State = wp.State,
                    PhaseCount = wp.Phases.Count,
                    TaskCount = wp.Phases.Sum(p => p.Tasks.Count),
                    CompletedTaskCount = wp.Phases.Sum(p => p.Tasks.Count(t => McpInputParser.IsTerminalState(t.State))),
                    CreatedAt = wp.CreatedAt,
                    ResolvedAt = wp.ResolvedAt
                };

                var activeWps = await apiClient.GetWorkPackagesByProjectAsync(projId, "active", ct);
                overview.ActiveWorkPackages = activeWps.Select(ToWpItem).ToList();

                var inactiveWps = await apiClient.GetWorkPackagesByProjectAsync(projId, "inactive", ct);
                overview.InactiveWorkPackages = inactiveWps.Select(ToWpItem).ToList();

                var wpSummary = await apiClient.GetWorkPackageSummaryAsync(projId, ct);
                overview.TerminalWorkPackageCount = wpSummary.TerminalCount;
            }
            catch
            {
                // Work package data is non-critical; proceed with overview
            }
        }

        return JsonSerializer.Serialize(overview, JsonDefaults.Indented);
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
