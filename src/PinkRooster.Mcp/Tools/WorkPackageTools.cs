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
public sealed class WorkPackageTools(PinkRoosterApiClient apiClient)
{
    // ── 1. get_work_packages ──

    [McpServerTool(Name = "get_work_packages", ReadOnly = true)]
    [Description("Returns a list of work packages for a project, optionally filtered by state category.")]
    public async Task<string> GetWorkPackages(
        [Description("Project ID in 'proj-{number}' format.")] string projectId,
        [Description("Filter by state category: 'active', 'inactive', 'terminal', or omit for all.")] string? stateFilter = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectId(projectId, out var projId))
            return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

        var workPackages = await apiClient.GetWorkPackagesByProjectAsync(projId, stateFilter, ct);

        if (workPackages.Count == 0)
            return OperationResult.SuccessMessage($"No work packages found for project '{projectId}'" +
                (stateFilter is not null ? $" with filter '{stateFilter}'." : "."));

        var items = workPackages.Select(wp => new WorkPackageOverviewItem
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
        }).ToList();

        return JsonSerializer.Serialize(items, JsonDefaults.Indented);
    }

    // ── 2. get_work_package_details ──

    [McpServerTool(Name = "get_work_package_details", ReadOnly = true)]
    [Description("Returns full details for a work package including phases, tasks, acceptance criteria, and dependencies.")]
    public async Task<string> GetWorkPackageDetails(
        [Description("Work package ID in 'proj-{number}-wp-{number}' format.")] string workPackageId,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseWorkPackageId(workPackageId, out var projId, out var wpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{workPackageId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        var wp = await apiClient.GetWorkPackageAsync(projId, wpNumber, ct);
        if (wp is null)
            return OperationResult.Warning($"Work package '{workPackageId}' not found.");

        var detail = new WorkPackageDetailResponse
        {
            WorkPackageId = wp.WorkPackageId,
            ProjectId = wp.ProjectId,
            Name = wp.Name,
            Description = wp.Description,
            Type = wp.Type,
            Priority = wp.Priority,
            Plan = wp.Plan,
            EstimatedComplexity = wp.EstimatedComplexity,
            EstimationRationale = wp.EstimationRationale,
            State = wp.State,
            PreviousActiveState = wp.PreviousActiveState,
            LinkedIssueId = wp.LinkedIssueId,
            StartedAt = wp.StartedAt,
            CompletedAt = wp.CompletedAt,
            ResolvedAt = wp.ResolvedAt,
            Attachments = McpInputParser.NullIfEmpty(wp.Attachments),
            Phases = wp.Phases.Select(MapPhaseDetail).ToList(),
            BlockedBy = McpInputParser.NullIfEmpty(wp.BlockedBy.Select(MapWpDependency).ToList()),
            Blocking = McpInputParser.NullIfEmpty(wp.Blocking.Select(MapWpDependency).ToList()),
            CreatedAt = wp.CreatedAt,
            UpdatedAt = wp.UpdatedAt
        };

        return JsonSerializer.Serialize(detail, JsonDefaults.Indented);
    }

    // ── 3. create_or_update_work_package ──

    [McpServerTool(Name = "create_or_update_work_package")]
    [Description(
        "Creates a new work package or updates an existing one. " +
        "To create: provide projectId and required fields (name, description). " +
        "To update: provide projectId and workPackageId, plus any fields to change.")]
    public async Task<string> CreateOrUpdateWorkPackage(
        [Description("Project ID in 'proj-{number}' format.")] string projectId,
        [Description("Work package ID in 'proj-{number}-wp-{number}' format. Omit to create a new work package.")] string? workPackageId = null,
        [Description("Work package name/title.")] string? name = null,
        [Description("Detailed description of the work package.")] string? description = null,
        [Description("Type: Feature, BugFix, Refactor, Spike, Chore")] string? type = null,
        [Description("Priority: Critical, High, Medium, Low")] string? priority = null,
        [Description("Implementation plan.")] string? plan = null,
        [Description("Estimated complexity (integer).")] string? estimatedComplexity = null,
        [Description("Rationale for the complexity estimation.")] string? estimationRationale = null,
        [Description("State: NotStarted, Designing, Implementing, Testing, InReview, Completed, Cancelled, Blocked, Replaced")] string? state = null,
        [Description("Linked issue ID in 'proj-{number}-issue-{number}' format.")] string? linkedIssueId = null,
        [Description("File attachments as JSON array: [{\"fileName\":\"...\",\"relativePath\":\"...\",\"description\":\"...\"}]")] string? attachments = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectId(projectId, out var projId))
            return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

        if (workPackageId is not null)
            return await UpdateExistingWorkPackage(projId, workPackageId, name, description, type,
                priority, plan, estimatedComplexity, estimationRationale, state, linkedIssueId, attachments, ct);

        return await CreateNewWorkPackage(projId, name, description, type, priority, plan,
            estimatedComplexity, estimationRationale, state, linkedIssueId, attachments, ct);
    }

    // ── 4. manage_work_package_dependency ──

    [McpServerTool(Name = "manage_work_package_dependency")]
    [Description("Adds or removes a dependency between work packages. The dependent work package is blocked by the depends-on work package.")]
    public async Task<string> ManageWorkPackageDependency(
        [Description("Dependent work package ID in 'proj-{number}-wp-{number}' format.")] string workPackageId,
        [Description("Depends-on work package ID in 'proj-{number}-wp-{number}' format.")] string dependsOnWorkPackageId,
        [Description("Action: 'add' or 'remove'.")] string action,
        [Description("Reason for the dependency.")] string? reason = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseWorkPackageId(workPackageId, out var projId, out var wpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{workPackageId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        if (!IdParser.TryParseWorkPackageId(dependsOnWorkPackageId, out var depProjId, out var depWpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{dependsOnWorkPackageId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        // Look up the depends-on WP to get its internal ID
        var dependsOnWp = await apiClient.GetWorkPackageAsync(depProjId, depWpNumber, ct);
        if (dependsOnWp is null)
            return OperationResult.Warning($"Depends-on work package '{dependsOnWorkPackageId}' not found.");

        switch (action.ToLowerInvariant())
        {
            case "add":
                var request = new ManageDependencyRequest
                {
                    DependsOnId = dependsOnWp.Id,
                    Reason = reason
                };
                DependencyResponse depResponse;
                try
                {
                    depResponse = await apiClient.AddWorkPackageDependencyAsync(projId, wpNumber, request, ct);
                }
                catch (HttpRequestException ex)
                {
                    return OperationResult.Error($"Failed to add dependency: {ex.Message}");
                }
                return OperationResult.Success(workPackageId,
                    $"Dependency added: '{workPackageId}' is now blocked by '{dependsOnWorkPackageId}'.",
                    stateChanges: depResponse.StateChanges);

            case "remove":
                var removed = await apiClient.RemoveWorkPackageDependencyAsync(projId, wpNumber, dependsOnWp.Id, ct);
                return removed
                    ? OperationResult.Success(workPackageId, $"Dependency removed: '{workPackageId}' is no longer blocked by '{dependsOnWorkPackageId}'.")
                    : OperationResult.Warning($"Dependency between '{workPackageId}' and '{dependsOnWorkPackageId}' not found.");

            default:
                return OperationResult.Error($"Invalid action: '{action}'. Expected 'add' or 'remove'.");
        }
    }

    // ── 5. scaffold_work_package ──

    [McpServerTool(Name = "scaffold_work_package")]
    [Description(
        "Creates a complete work package with phases, tasks, acceptance criteria, and task dependencies in a single call. " +
        "Tasks require name + description; all other fields are optional. " +
        "Task dependencies use 0-based indices within the same phase's task array via dependsOnTaskIndices. " +
        "Returns a compact ID map of all created entities.")]
    public async Task<string> ScaffoldWorkPackage(
        [Description("Project ID in 'proj-{number}' format.")] string projectId,
        [Description("Work package name/title.")] string name,
        [Description("Detailed description of the work package.")] string description,
        [Description("Phases with optional tasks, acceptance criteria, and task dependencies.")] List<ScaffoldPhaseRequest> phases,
        [Description("Type: Feature, BugFix, Refactor, Spike, Chore (default: Feature)")] string? type = null,
        [Description("Priority: Critical, High, Medium, Low (default: Medium)")] string? priority = null,
        [Description("Implementation plan (markdown).")] string? plan = null,
        [Description("Estimated complexity (integer).")] string? estimatedComplexity = null,
        [Description("Rationale for the complexity estimation.")] string? estimationRationale = null,
        [Description("State: NotStarted, Designing, Implementing, Testing, InReview, Completed, Cancelled, Blocked, Replaced")] string? state = null,
        [Description("Linked issue ID in 'proj-{number}-issue-{number}' format.")] string? linkedIssueId = null,
        [Description("Existing WP IDs that block this WP, as JSON array: [\"proj-1-wp-2\"]")] string? blockedByWorkPackageIds = null,
        [Description("File attachments as JSON array: [{\"fileName\":\"...\",\"relativePath\":\"...\",\"description\":\"...\"}]")] string? attachments = null,
        CancellationToken ct = default)
    {
        try
        {
            if (!IdParser.TryParseProjectId(projectId, out var projId))
                return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

            if (phases.Count == 0)
                return OperationResult.Error("'phases' must contain at least one phase.");

            var request = new ScaffoldWorkPackageRequest
            {
                Name = name,
                Description = description,
                Type = McpInputParser.ParseEnumOrDefault(type, WorkPackageType.Feature),
                Priority = McpInputParser.ParseEnumOrDefault(priority, Priority.Medium),
                Plan = plan,
                EstimatedComplexity = McpInputParser.ParseInt(estimatedComplexity),
                EstimationRationale = estimationRationale,
                State = McpInputParser.ParseEnumOrDefault(state, CompletionState.NotStarted),
                Attachments = McpInputParser.ParseFileReferences(attachments),
                Phases = phases
            };

            // Resolve linked issue ID
            if (linkedIssueId is not null)
            {
                if (!IdParser.TryParseIssueId(linkedIssueId, out var issueProjId, out var issueNumber))
                    return OperationResult.Error($"Invalid linked issue ID format: '{linkedIssueId}'. Expected 'proj-{{number}}-issue-{{number}}'.");

                var issue = await apiClient.GetIssueAsync(issueProjId, issueNumber, ct);
                if (issue is null)
                    return OperationResult.Warning($"Linked issue '{linkedIssueId}' not found.");

                request.LinkedIssueId = issue.Id;
            }

            // Resolve blockedBy WP IDs
            if (!string.IsNullOrWhiteSpace(blockedByWorkPackageIds))
            {
                List<string>? blockerIdStrings;
                try
                {
                    blockerIdStrings = JsonSerializer.Deserialize<List<string>>(blockedByWorkPackageIds, JsonDefaults.Indented);
                }
                catch
                {
                    return OperationResult.Error("'blockedByWorkPackageIds' must be a valid JSON array of WP ID strings.");
                }

                if (blockerIdStrings is { Count: > 0 })
                {
                    request.BlockedByWpIds = [];
                    foreach (var wpIdStr in blockerIdStrings)
                    {
                        if (!IdParser.TryParseWorkPackageId(wpIdStr, out var bProjId, out var bWpNumber))
                            return OperationResult.Error($"Invalid blocker WP ID format: '{wpIdStr}'. Expected 'proj-{{number}}-wp-{{number}}'.");

                        var blockerWp = await apiClient.GetWorkPackageAsync(bProjId, bWpNumber, ct);
                        if (blockerWp is null)
                            return OperationResult.Warning($"Blocker work package '{wpIdStr}' not found.");

                        request.BlockedByWpIds.Add(blockerWp.Id);
                    }
                }
            }

            var result = await apiClient.ScaffoldWorkPackageAsync(projId, request, ct);

            var totalTasks = result.Phases.Sum(p => p.TaskIds.Count);
            var response = new ScaffoldOperationResult
            {
                ResponseType = ResponseType.Success,
                Message = $"Work package '{result.WorkPackageId}' scaffolded: {result.Phases.Count} phases, {totalTasks} tasks, {result.TotalDependencies} dependencies.",
                Id = result.WorkPackageId,
                Phases = result.Phases,
                TotalTasks = totalTasks,
                TotalDependencies = result.TotalDependencies,
                StateChanges = result.StateChanges
            };

            return JsonSerializer.Serialize(response, JsonDefaults.Indented);
        }
        catch (HttpRequestException ex)
        {
            return OperationResult.Error($"Scaffold failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Unexpected error during scaffold: {ex.Message}");
        }
    }

    // ── Private helpers: Work Package create/update ──

    private async Task<string> CreateNewWorkPackage(
        long projId, string? name, string? description, string? type, string? priority,
        string? plan, string? estimatedComplexity, string? estimationRationale, string? state,
        string? linkedIssueId, string? attachments, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Error("'name' is required when creating a work package.");
        if (string.IsNullOrWhiteSpace(description))
            return OperationResult.Error("'description' is required when creating a work package.");

        var request = new CreateWorkPackageRequest
        {
            Name = name,
            Description = description,
            Type = McpInputParser.ParseEnumOrDefault(type, WorkPackageType.Feature),
            Priority = McpInputParser.ParseEnumOrDefault(priority, Priority.Medium),
            Plan = plan,
            EstimatedComplexity = McpInputParser.ParseInt(estimatedComplexity),
            EstimationRationale = estimationRationale,
            State = McpInputParser.ParseEnumOrDefault(state, CompletionState.NotStarted),
            Attachments = McpInputParser.ParseFileReferences(attachments)
        };

        // Resolve linked issue ID if provided
        if (linkedIssueId is not null)
        {
            if (!IdParser.TryParseIssueId(linkedIssueId, out var issueProjId, out var issueNumber))
                return OperationResult.Error($"Invalid linked issue ID format: '{linkedIssueId}'. Expected 'proj-{{number}}-issue-{{number}}'.");

            var issue = await apiClient.GetIssueAsync(issueProjId, issueNumber, ct);
            if (issue is null)
                return OperationResult.Warning($"Linked issue '{linkedIssueId}' not found.");

            request.LinkedIssueId = issue.Id;
        }

        var created = await apiClient.CreateWorkPackageAsync(projId, request, ct);
        return OperationResult.Success(created.WorkPackageId, $"Work package '{name}' created.");
    }

    private async Task<string> UpdateExistingWorkPackage(
        long projId, string workPackageId, string? name, string? description, string? type,
        string? priority, string? plan, string? estimatedComplexity, string? estimationRationale,
        string? state, string? linkedIssueId, string? attachments, CancellationToken ct)
    {
        if (!IdParser.TryParseWorkPackageId(workPackageId, out var parsedProjId, out var wpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{workPackageId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        if (parsedProjId != projId)
            return OperationResult.Error($"Work package ID '{workPackageId}' does not belong to project 'proj-{projId}'.");

        var request = new UpdateWorkPackageRequest
        {
            Name = name,
            Description = description,
            Type = type is not null ? McpInputParser.ParseEnum<WorkPackageType>(type) : null,
            Priority = priority is not null ? McpInputParser.ParseEnum<Priority>(priority) : null,
            Plan = plan,
            EstimatedComplexity = McpInputParser.ParseInt(estimatedComplexity),
            EstimationRationale = estimationRationale,
            State = state is not null ? McpInputParser.ParseEnum<CompletionState>(state) : null,
            Attachments = attachments is not null ? McpInputParser.ParseFileReferences(attachments) : null
        };

        // Resolve linked issue ID if provided
        if (linkedIssueId is not null)
        {
            if (!IdParser.TryParseIssueId(linkedIssueId, out var issueProjId, out var issueNumber))
                return OperationResult.Error($"Invalid linked issue ID format: '{linkedIssueId}'. Expected 'proj-{{number}}-issue-{{number}}'.");

            var issue = await apiClient.GetIssueAsync(issueProjId, issueNumber, ct);
            if (issue is null)
                return OperationResult.Warning($"Linked issue '{linkedIssueId}' not found.");

            request.LinkedIssueId = issue.Id;
        }

        var updated = await apiClient.UpdateWorkPackageAsync(projId, wpNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Work package '{workPackageId}' not found.");

        return OperationResult.Success(workPackageId, $"Work package '{workPackageId}' updated.",
            stateChanges: updated.StateChanges);
    }

    // ── Mapping helpers (WP detail tree) ──

    private static PhaseDetailItem MapPhaseDetail(PhaseResponse phase) => new()
    {
        PhaseId = phase.PhaseId,
        Name = phase.Name,
        Description = phase.Description,
        SortOrder = phase.SortOrder,
        State = phase.State,
        AcceptanceCriteria = McpInputParser.NullIfEmpty(phase.AcceptanceCriteria.Select(ac => new AcceptanceCriterionItem
        {
            Name = ac.Name,
            Description = ac.Description,
            VerificationMethod = ac.VerificationMethod.ToString(),
            VerificationResult = ac.VerificationResult,
            VerifiedAt = ac.VerifiedAt
        }).ToList()),
        Tasks = phase.Tasks.Select(MapTaskDetail).ToList()
    };

    private static TaskDetailItem MapTaskDetail(TaskResponse task) => new()
    {
        TaskId = task.TaskId,
        Name = task.Name,
        Description = task.Description,
        SortOrder = task.SortOrder,
        ImplementationNotes = task.ImplementationNotes,
        State = task.State,
        PreviousActiveState = task.PreviousActiveState,
        StartedAt = task.StartedAt,
        CompletedAt = task.CompletedAt,
        ResolvedAt = task.ResolvedAt,
        TargetFiles = McpInputParser.NullIfEmpty(task.TargetFiles),
        Attachments = McpInputParser.NullIfEmpty(task.Attachments),
        BlockedBy = McpInputParser.NullIfEmpty(task.BlockedBy.Select(MapTaskDependency).ToList()),
        Blocking = McpInputParser.NullIfEmpty(task.Blocking.Select(MapTaskDependency).ToList())
    };

    private static DependencyItem MapWpDependency(DependencyResponse dep) => new()
    {
        EntityId = dep.WorkPackageId,
        Name = dep.Name,
        State = dep.State,
        Reason = dep.Reason
    };

    private static DependencyItem MapTaskDependency(TaskDependencyResponse dep) => new()
    {
        EntityId = dep.TaskId,
        Name = dep.Name,
        State = dep.State,
        Reason = dep.Reason
    };
}
