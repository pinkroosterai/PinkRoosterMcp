using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Helpers;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class IssueTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "add_or_update_issue")]
    [Description(
        "Creates a new issue or updates an existing one. " +
        "To create: provide projectId and required fields (name, description, issueType, severity). " +
        "To update: provide projectId and issueId, plus any fields to change.")]
    public async Task<string> AddOrUpdateIssue(
        [Description("Project ID in 'proj-{number}' format.")] string projectId,
        [Description("Issue ID in 'proj-{number}-issue-{number}' format. Omit to create a new issue.")] string? issueId = null,
        [Description("Issue name/title.")] string? name = null,
        [Description("Detailed description of the issue.")] string? description = null,
        [Description("Type: Bug, Defect, Regression, TechnicalDebt, PerformanceIssue, SecurityVulnerability")] string? issueType = null,
        [Description("Severity: Critical, Major, Minor, Trivial")] string? severity = null,
        [Description("Priority: Critical, High, Medium, Low")] string? priority = null,
        [Description("State: NotStarted, Designing, Implementing, Testing, InReview, Completed, Cancelled, Blocked, Replaced")] string? state = null,
        [Description("Steps to reproduce the issue.")] string? stepsToReproduce = null,
        [Description("Expected behavior.")] string? expectedBehavior = null,
        [Description("Actual behavior observed.")] string? actualBehavior = null,
        [Description("Affected file, module, or area.")] string? affectedComponent = null,
        [Description("Stack trace or error output.")] string? stackTrace = null,
        [Description("Root cause analysis.")] string? rootCause = null,
        [Description("Resolution description.")] string? resolution = null,
        [Description("File attachments as JSON array: [{\"fileName\":\"...\",\"relativePath\":\"...\",\"description\":\"...\"}]")] string? attachments = null,
        CancellationToken ct = default)
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

    [McpServerTool(Name = "get_issue_details", ReadOnly = true)]
    [Description("Returns full details for a specific issue.")]
    public async Task<string> GetIssueDetails(
        [Description("Issue ID in 'proj-{number}-issue-{number}' format.")] string issueId,
        CancellationToken ct = default)
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

    [McpServerTool(Name = "get_issue_overview", ReadOnly = true)]
    [Description("Returns a list of issues for a project, optionally filtered by state category.")]
    public async Task<string> GetIssueOverview(
        [Description("Project ID in 'proj-{number}' format.")] string projectId,
        [Description("Filter by state category: 'active', 'inactive', 'terminal', or omit for all.")] string? stateFilter = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectId(projectId, out var projId))
            return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

        var issues = await apiClient.GetIssuesByProjectAsync(projId, stateFilter, ct);

        if (issues.Count == 0)
            return OperationResult.SuccessMessage($"No issues found for project '{projectId}'" +
                (stateFilter is not null ? $" with filter '{stateFilter}'." : "."));

        var items = issues.Select(i => new IssueOverviewItem
        {
            IssueId = i.IssueId,
            Name = i.Name,
            State = i.State,
            Priority = i.Priority,
            Severity = i.Severity,
            IssueType = i.IssueType,
            LinkedWorkPackageCount = i.LinkedWorkPackages.Count,
            CreatedAt = i.CreatedAt
        }).ToList();

        return JsonSerializer.Serialize(items, JsonDefaults.Indented);
    }

    // ── Private helpers ──

    private async Task<string> CreateNewIssue(
        long projId, string? name, string? description, string? issueType, string? severity,
        string? priority, string? state, string? stepsToReproduce, string? expectedBehavior,
        string? actualBehavior, string? affectedComponent, string? stackTrace,
        string? rootCause, string? resolution, string? attachments, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Error("'name' is required when creating an issue.");
        if (string.IsNullOrWhiteSpace(description))
            return OperationResult.Error("'description' is required when creating an issue.");
        if (string.IsNullOrWhiteSpace(issueType))
            return OperationResult.Error("'issueType' is required when creating an issue.");
        if (string.IsNullOrWhiteSpace(severity))
            return OperationResult.Error("'severity' is required when creating an issue.");

        if (!Enum.TryParse<IssueType>(issueType, true, out var parsedType))
            return OperationResult.Error($"Invalid issueType: '{issueType}'.");
        if (!Enum.TryParse<IssueSeverity>(severity, true, out var parsedSeverity))
            return OperationResult.Error($"Invalid severity: '{severity}'.");

        var request = new CreateIssueRequest
        {
            Name = name,
            Description = description,
            IssueType = parsedType,
            Severity = parsedSeverity,
            Priority = McpInputParser.ParseEnumOrDefault(priority, Priority.Medium),
            State = McpInputParser.ParseEnumOrDefault(state, CompletionState.NotStarted),
            StepsToReproduce = stepsToReproduce,
            ExpectedBehavior = expectedBehavior,
            ActualBehavior = actualBehavior,
            AffectedComponent = affectedComponent,
            StackTrace = stackTrace,
            RootCause = rootCause,
            Resolution = resolution,
            Attachments = McpInputParser.ParseFileReferences(attachments)
        };

        var created = await apiClient.CreateIssueAsync(projId, request, ct);
        return OperationResult.Success(created.IssueId, $"Issue '{name}' created.");
    }

    private async Task<string> UpdateExistingIssue(
        long projId, string issueId, string? name, string? description, string? issueType,
        string? severity, string? priority, string? state, string? stepsToReproduce,
        string? expectedBehavior, string? actualBehavior, string? affectedComponent,
        string? stackTrace, string? rootCause, string? resolution, string? attachments,
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
            IssueType = issueType is not null ? McpInputParser.ParseEnum<IssueType>(issueType) : null,
            Severity = severity is not null ? McpInputParser.ParseEnum<IssueSeverity>(severity) : null,
            Priority = priority is not null ? McpInputParser.ParseEnum<Priority>(priority) : null,
            State = state is not null ? McpInputParser.ParseEnum<CompletionState>(state) : null,
            StepsToReproduce = stepsToReproduce,
            ExpectedBehavior = expectedBehavior,
            ActualBehavior = actualBehavior,
            AffectedComponent = affectedComponent,
            StackTrace = stackTrace,
            RootCause = rootCause,
            Resolution = resolution,
            Attachments = attachments is not null ? McpInputParser.ParseFileReferences(attachments) : null
        };

        var updated = await apiClient.UpdateIssueAsync(projId, issueNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Issue '{issueId}' not found.");

        return OperationResult.Success(issueId, $"Issue '{issueId}' updated.");
    }
}
