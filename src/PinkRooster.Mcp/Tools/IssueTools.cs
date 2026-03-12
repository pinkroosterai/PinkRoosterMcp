using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Helpers;
using PinkRooster.Mcp.Inputs;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class IssueTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "create_or_update_issue",
        Title = "Create or Update Issue", Destructive = false, OpenWorld = false)]
    [Description(
        "Creates a new issue or updates an existing one. " +
        "To create: provide projectId and required fields (name, description, issueType, severity). " +
        "To update: provide projectId and issueId, plus any fields to change (PATCH semantics: null = keep current). " +
        "Returns OperationResult with the issue ID (e.g. 'proj-1-issue-5'). " +
        "Issues track bugs and problems — use create_or_update_work_package for planned work. " +
        "Does NOT delete issues — use delete_entity for that.")]
    public async Task<string> CreateOrUpdateIssue(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Issue ID (e.g. 'proj-1-issue-3'). Omit to create a new issue.")] string? issueId = null,
        [Description("Issue name/title.")] string? name = null,
        [Description("Detailed description of the issue.")] string? description = null,
        [Description("Issue type. Required for create, optional for update.")] IssueType? issueType = null,
        [Description("Issue severity. Required for create, optional for update.")] IssueSeverity? severity = null,
        [Description("Priority level. Default: Medium.")] Priority? priority = null,
        [Description("Completion state (e.g. NotStarted, Implementing, Completed). Omit to keep current.")] CompletionState? state = null,
        [Description("Steps to reproduce the issue.")] string? stepsToReproduce = null,
        [Description("Expected behavior.")] string? expectedBehavior = null,
        [Description("Actual behavior observed.")] string? actualBehavior = null,
        [Description("Affected file, module, or area (e.g. 'src/Services/IssueService.cs' or 'Authentication').")] string? affectedComponent = null,
        [Description("Stack trace or error output.")] string? stackTrace = null,
        [Description("Root cause analysis.")] string? rootCause = null,
        [Description("Resolution description.")] string? resolution = null,
        [Description("File attachments.")] List<FileReferenceInput>? attachments = null,
        CancellationToken ct = default)
    {
        try
        {
            if (!IdParser.TryParseProjectId(projectId, out var projId))
                return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

            if (issueId is not null)
                return await UpdateExistingIssue(projId, issueId, name, description, issueType, severity,
                    priority, state, stepsToReproduce, expectedBehavior, actualBehavior,
                    affectedComponent, stackTrace, rootCause, resolution, attachments, ct);

            return await CreateNewIssue(projId, name, description, issueType, severity,
                priority, state, stepsToReproduce, expectedBehavior, actualBehavior,
                affectedComponent, stackTrace, rootCause, resolution, attachments, ct);
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to create/update issue: {ex.Message}");
        }
    }

    [McpServerTool(Name = "get_issue_details", ReadOnly = true,
        Title = "Get Issue Details", OpenWorld = false)]
    [Description(
        "Returns all fields for a single issue including state timestamps, attachments, and linked work packages. " +
        "Does NOT include audit history. " +
        "For listing multiple issues, use get_issue_overview instead.")]
    public async Task<string> GetIssueDetails(
        [Description("Issue ID (e.g. 'proj-1-issue-3').")] string issueId,
        CancellationToken ct = default)
    {
        try
        {
            if (!IdParser.TryParseIssueId(issueId, out var projId, out var issueNumber))
                return OperationResult.Error($"Invalid issue ID format: '{issueId}'. Expected 'proj-{{number}}-issue-{{number}}'.");

            var issue = await apiClient.GetIssueAsync(projId, issueNumber, ct);
            if (issue is null)
                return OperationResult.Warning($"Issue '{issueId}' not found.");

            var detail = new IssueDetailResponse
            {
                IssueId = issue.IssueId,
                ProjectId = issue.ProjectId,
                Name = issue.Name,
                Description = issue.Description,
                IssueType = issue.IssueType,
                Severity = issue.Severity,
                Priority = issue.Priority,
                State = issue.State,
                StepsToReproduce = issue.StepsToReproduce,
                ExpectedBehavior = issue.ExpectedBehavior,
                ActualBehavior = issue.ActualBehavior,
                AffectedComponent = issue.AffectedComponent,
                StackTrace = issue.StackTrace,
                RootCause = issue.RootCause,
                Resolution = issue.Resolution,
                Attachments = issue.Attachments,
                LinkedWorkPackages = McpInputParser.NullIfEmpty(issue.LinkedWorkPackages),
                StartedAt = issue.StartedAt,
                CompletedAt = issue.CompletedAt,
                ResolvedAt = issue.ResolvedAt,
                CreatedAt = issue.CreatedAt,
                UpdatedAt = issue.UpdatedAt
            };

            return JsonSerializer.Serialize(detail, JsonDefaults.Indented);
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to get issue details: {ex.Message}");
        }
    }

    [McpServerTool(Name = "get_issue_overview", ReadOnly = true,
        Title = "Get Issue Overview", OpenWorld = false)]
    [Description(
        "Returns a compact list of issues (ID, name, state, priority, severity) for a project. " +
        "Does NOT include detailed fields (steps to reproduce, stack trace, attachments, linked WPs). " +
        "For full issue data, use get_issue_details instead. " +
        "For issue counts by category, use get_project_status.")]
    public async Task<string> GetIssueOverview(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Filter by state category. Omit for all issues.")] StateFilterCategory? stateFilter = null,
        CancellationToken ct = default)
    {
        try
        {
            if (!IdParser.TryParseProjectId(projectId, out var projId))
                return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

            var stateFilterStr = stateFilter?.ToString().ToLowerInvariant();
            var issues = await apiClient.GetIssuesByProjectAsync(projId, stateFilterStr, ct);

            if (issues.Count == 0)
                return OperationResult.SuccessMessage($"No issues found for project '{projectId}'" +
                    (stateFilter is not null ? $" with filter '{stateFilter}'." : "."));

            var items = issues.Select(i => new
            {
                i.IssueId,
                i.Name,
                i.State,
                i.Priority,
                i.Severity,
                i.IssueType,
                LinkedWorkPackageCount = i.LinkedWorkPackages.Count,
                i.CreatedAt
            }).ToList();

            return JsonSerializer.Serialize(items, JsonDefaults.Indented);
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to get issue overview: {ex.Message}");
        }
    }

    // ── Private helpers ──

    private async Task<string> CreateNewIssue(
        long projId, string? name, string? description, IssueType? issueType, IssueSeverity? severity,
        Priority? priority, CompletionState? state, string? stepsToReproduce, string? expectedBehavior,
        string? actualBehavior, string? affectedComponent, string? stackTrace,
        string? rootCause, string? resolution, List<FileReferenceInput>? attachments, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Error("'name' is required when creating an issue.");
        if (string.IsNullOrWhiteSpace(description))
            return OperationResult.Error("'description' is required when creating an issue.");
        if (issueType is null)
            return OperationResult.Error("'issueType' is required when creating an issue.");
        if (severity is null)
            return OperationResult.Error("'severity' is required when creating an issue.");

        var request = new CreateIssueRequest
        {
            Name = name,
            Description = description,
            IssueType = issueType.Value,
            Severity = severity.Value,
            Priority = priority ?? Priority.Medium,
            State = state ?? CompletionState.NotStarted,
            StepsToReproduce = stepsToReproduce,
            ExpectedBehavior = expectedBehavior,
            ActualBehavior = actualBehavior,
            AffectedComponent = affectedComponent,
            StackTrace = stackTrace,
            RootCause = rootCause,
            Resolution = resolution,
            Attachments = McpInputParser.MapFileReferences(attachments)
        };

        var created = await apiClient.CreateIssueAsync(projId, request, ct);
        return OperationResult.Success(created.IssueId, $"Issue '{name}' created.");
    }

    private async Task<string> UpdateExistingIssue(
        long projId, string issueId, string? name, string? description, IssueType? issueType,
        IssueSeverity? severity, Priority? priority, CompletionState? state, string? stepsToReproduce,
        string? expectedBehavior, string? actualBehavior, string? affectedComponent,
        string? stackTrace, string? rootCause, string? resolution, List<FileReferenceInput>? attachments,
        CancellationToken ct)
    {
        if (!IdParser.TryParseIssueId(issueId, out var parsedProjId, out var issueNumber))
            return OperationResult.Error($"Invalid issue ID format: '{issueId}'. Expected 'proj-{{number}}-issue-{{number}}'.");

        if (parsedProjId != projId)
            return OperationResult.Error($"Issue ID '{issueId}' does not belong to project 'proj-{projId}'.");

        var request = new UpdateIssueRequest
        {
            Name = name,
            Description = description,
            IssueType = issueType,
            Severity = severity,
            Priority = priority,
            State = state,
            StepsToReproduce = stepsToReproduce,
            ExpectedBehavior = expectedBehavior,
            ActualBehavior = actualBehavior,
            AffectedComponent = affectedComponent,
            StackTrace = stackTrace,
            RootCause = rootCause,
            Resolution = resolution,
            Attachments = attachments is not null ? McpInputParser.MapFileReferences(attachments) : null
        };

        var updated = await apiClient.UpdateIssueAsync(projId, issueNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Issue '{issueId}' not found.");

        return OperationResult.Success(issueId, $"Issue '{issueId}' updated.");
    }
}
