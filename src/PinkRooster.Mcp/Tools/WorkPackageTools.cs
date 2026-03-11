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
public sealed class WorkPackageTools(PinkRoosterApiClient apiClient)
{
    // ── 1. get_work_packages ──

    [McpServerTool(Name = "get_work_packages", ReadOnly = true,
        Title = "Get Work Packages", OpenWorld = false)]
    [Description(
        "Returns a compact list of work packages (ID, name, state, task counts) for a project. " +
        "For full WP tree (phases, tasks, dependencies), use get_work_package_details. " +
        "For WP counts by category, use get_project_status.")]
    public async Task<string> GetWorkPackages(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Filter by state category. Omit for all work packages.")] StateFilterCategory? stateFilter = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectId(projectId, out var projId))
            return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

        var stateFilterStr = stateFilter?.ToString().ToLowerInvariant();
        var workPackages = await apiClient.GetWorkPackagesByProjectAsync(projId, stateFilterStr, ct);

        if (workPackages.Count == 0)
            return OperationResult.SuccessMessage($"No work packages found for project '{projectId}'" +
                (stateFilter is not null ? $" with filter '{stateFilter}'." : "."));

        var items = workPackages.Select(wp => new
        {
            wp.WorkPackageId,
            wp.Name,
            wp.Type,
            wp.Priority,
            wp.State,
            PhaseCount = wp.Phases.Count,
            TaskCount = wp.Phases.Sum(p => p.Tasks.Count),
            CompletedTaskCount = wp.Phases.Sum(p => p.Tasks.Count(t => McpInputParser.IsTerminalState(t.State))),
            wp.CreatedAt,
            wp.ResolvedAt
        }).ToList();

        return JsonSerializer.Serialize(items, JsonDefaults.Indented);
    }

    // ── 2. get_work_package_details ──

    [McpServerTool(Name = "get_work_package_details", ReadOnly = true,
        Title = "Get Work Package Details", OpenWorld = false)]
    [Description(
        "Returns all fields for a single work package including phases, tasks, acceptance criteria, " +
        "and dependencies. Use get_work_packages for a compact list first.")]
    public async Task<string> GetWorkPackageDetails(
        [Description("Work package ID (e.g. 'proj-1-wp-2').")] string workPackageId,
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
            LinkedFeatureRequestId = wp.LinkedFeatureRequestId,
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

    [McpServerTool(Name = "create_or_update_work_package",
        Title = "Create or Update Work Package", Destructive = false, OpenWorld = false)]
    [Description(
        "Creates a new work package or updates an existing one. Returns the WP ID and any cascade state changes. " +
        "To create: provide projectId and required fields (name, description). " +
        "To update: provide projectId and workPackageId, plus any fields to change. " +
        "For creating a complete WP with phases and tasks in one call, use scaffold_work_package instead.")]
    public async Task<string> CreateOrUpdateWorkPackage(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Work package ID (e.g. 'proj-1-wp-2'). Omit to create a new work package.")] string? workPackageId = null,
        [Description("Work package name/title.")] string? name = null,
        [Description("Detailed description of the work package.")] string? description = null,
        [Description("Work package type. Default: Feature.")] WorkPackageType? type = null,
        [Description("Priority level. Default: Medium.")] Priority? priority = null,
        [Description("Implementation plan (supports markdown).")] string? plan = null,
        [Description("Estimated complexity (1-10 scale).")] int? estimatedComplexity = null,
        [Description("Rationale for the complexity estimation.")] string? estimationRationale = null,
        [Description("Completion state (e.g. NotStarted, Implementing, Completed). Omit to keep current.")] CompletionState? state = null,
        [Description("Linked issue ID (e.g. 'proj-1-issue-3').")] string? linkedIssueId = null,
        [Description("Linked feature request ID (e.g. 'proj-1-fr-1').")] string? linkedFeatureRequestId = null,
        [Description("File attachments.")] List<FileReferenceInput>? attachments = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseProjectId(projectId, out var projId))
            return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

        if (workPackageId is not null)
            return await UpdateExistingWorkPackage(projId, workPackageId, name, description, type,
                priority, plan, estimatedComplexity, estimationRationale, state, linkedIssueId, linkedFeatureRequestId, attachments, ct);

        return await CreateNewWorkPackage(projId, name, description, type, priority, plan,
            estimatedComplexity, estimationRationale, state, linkedIssueId, linkedFeatureRequestId, attachments, ct);
    }

    // ── 4. manage_work_package_dependency ──

    [McpServerTool(Name = "manage_work_package_dependency",
        Title = "Manage Work Package Dependency", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Adds or removes a dependency between work packages. " +
        "When adding: if the blocker is non-terminal, the dependent auto-transitions to Blocked. " +
        "When the blocker completes, dependents auto-unblock. " +
        "Returns stateChanges showing any automatic state transitions.")]
    public async Task<string> ManageWorkPackageDependency(
        [Description("Dependent work package ID (e.g. 'proj-1-wp-3').")] string workPackageId,
        [Description("Blocker work package ID (e.g. 'proj-1-wp-1').")] string dependsOnWorkPackageId,
        [Description("Whether to add or remove the dependency.")] DependencyAction action,
        [Description("Reason for the dependency.")] string? reason = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseWorkPackageId(workPackageId, out var projId, out var wpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{workPackageId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        if (!IdParser.TryParseWorkPackageId(dependsOnWorkPackageId, out var depProjId, out var depWpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{dependsOnWorkPackageId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        var dependsOnWp = await apiClient.GetWorkPackageAsync(depProjId, depWpNumber, ct);
        if (dependsOnWp is null)
            return OperationResult.Warning($"Depends-on work package '{dependsOnWorkPackageId}' not found.");

        switch (action)
        {
            case DependencyAction.Add:
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

            case DependencyAction.Remove:
                var removed = await apiClient.RemoveWorkPackageDependencyAsync(projId, wpNumber, dependsOnWp.Id, ct);
                return removed
                    ? OperationResult.Success(workPackageId, $"Dependency removed: '{workPackageId}' is no longer blocked by '{dependsOnWorkPackageId}'.")
                    : OperationResult.Warning($"Dependency between '{workPackageId}' and '{dependsOnWorkPackageId}' not found.");

            default:
                return OperationResult.Error($"Invalid action: '{action}'.");
        }
    }

    // ── 5. scaffold_work_package ──

    [McpServerTool(Name = "scaffold_work_package",
        Title = "Scaffold Work Package", Destructive = false, OpenWorld = false)]
    [Description(
        "Creates a complete work package with phases, tasks, acceptance criteria, and task dependencies in a single call. " +
        "Tasks require name + description; all other fields are optional. " +
        "Task dependencies use 0-based indices within the same phase's task array via dependsOnTaskIndices. " +
        "Returns a compact ID map of all created entities. " +
        "For creating/updating a WP without phases or tasks, use create_or_update_work_package instead.")]
    public async Task<string> ScaffoldWorkPackage(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Work package name/title.")] string name,
        [Description("Detailed description of the work package.")] string description,
        [Description("Phases with optional tasks, acceptance criteria, and task dependencies.")] List<ScaffoldPhaseInput> phases,
        [Description("Work package type. Default: Feature.")] WorkPackageType? type = null,
        [Description("Priority level. Default: Medium.")] Priority? priority = null,
        [Description("Implementation plan (supports markdown).")] string? plan = null,
        [Description("Estimated complexity (1-10 scale).")] int? estimatedComplexity = null,
        [Description("Rationale for the complexity estimation.")] string? estimationRationale = null,
        [Description("Completion state (e.g. NotStarted, Implementing). Default: NotStarted.")] CompletionState? state = null,
        [Description("Linked issue ID (e.g. 'proj-1-issue-3').")] string? linkedIssueId = null,
        [Description("Linked feature request ID (e.g. 'proj-1-fr-1').")] string? linkedFeatureRequestId = null,
        [Description("Existing WP IDs that block this WP (e.g. ['proj-1-wp-1']).")] List<string>? blockedByWorkPackageIds = null,
        [Description("File attachments.")] List<FileReferenceInput>? attachments = null,
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
                Type = type ?? WorkPackageType.Feature,
                Priority = priority ?? Priority.Medium,
                Plan = plan,
                EstimatedComplexity = estimatedComplexity,
                EstimationRationale = estimationRationale,
                State = state ?? CompletionState.NotStarted,
                Attachments = McpInputParser.MapFileReferences(attachments),
                Phases = McpInputParser.MapScaffoldPhases(phases)
            };

            if (linkedIssueId is not null)
            {
                if (!IdParser.TryParseIssueId(linkedIssueId, out var issueProjId, out var issueNumber))
                    return OperationResult.Error($"Invalid linked issue ID format: '{linkedIssueId}'. Expected 'proj-{{number}}-issue-{{number}}'.");

                var issue = await apiClient.GetIssueAsync(issueProjId, issueNumber, ct);
                if (issue is null)
                    return OperationResult.Warning($"Linked issue '{linkedIssueId}' not found.");

                request.LinkedIssueId = issue.Id;
            }

            if (linkedFeatureRequestId is not null)
            {
                if (!IdParser.TryParseFeatureRequestId(linkedFeatureRequestId, out var frProjId, out var frNumber))
                    return OperationResult.Error($"Invalid linked feature request ID format: '{linkedFeatureRequestId}'. Expected 'proj-{{number}}-fr-{{number}}'.");

                var fr = await apiClient.GetFeatureRequestAsync(frProjId, frNumber, ct);
                if (fr is null)
                    return OperationResult.Warning($"Linked feature request '{linkedFeatureRequestId}' not found.");

                request.LinkedFeatureRequestId = fr.Id;
            }

            if (blockedByWorkPackageIds is { Count: > 0 })
            {
                request.BlockedByWpIds = [];
                foreach (var wpIdStr in blockedByWorkPackageIds)
                {
                    if (!IdParser.TryParseWorkPackageId(wpIdStr, out var bProjId, out var bWpNumber))
                        return OperationResult.Error($"Invalid blocker WP ID format: '{wpIdStr}'. Expected 'proj-{{number}}-wp-{{number}}'.");

                    var blockerWp = await apiClient.GetWorkPackageAsync(bProjId, bWpNumber, ct);
                    if (blockerWp is null)
                        return OperationResult.Warning($"Blocker work package '{wpIdStr}' not found.");

                    request.BlockedByWpIds.Add(blockerWp.Id);
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

    // ── Private helpers ──

    private async Task<string> CreateNewWorkPackage(
        long projId, string? name, string? description, WorkPackageType? type, Priority? priority,
        string? plan, int? estimatedComplexity, string? estimationRationale, CompletionState? state,
        string? linkedIssueId, string? linkedFeatureRequestId, List<FileReferenceInput>? attachments, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Error("'name' is required when creating a work package.");
        if (string.IsNullOrWhiteSpace(description))
            return OperationResult.Error("'description' is required when creating a work package.");

        var request = new CreateWorkPackageRequest
        {
            Name = name,
            Description = description,
            Type = type ?? WorkPackageType.Feature,
            Priority = priority ?? Priority.Medium,
            Plan = plan,
            EstimatedComplexity = estimatedComplexity,
            EstimationRationale = estimationRationale,
            State = state ?? CompletionState.NotStarted,
            Attachments = McpInputParser.MapFileReferences(attachments)
        };

        if (linkedIssueId is not null)
        {
            if (!IdParser.TryParseIssueId(linkedIssueId, out var issueProjId, out var issueNumber))
                return OperationResult.Error($"Invalid linked issue ID format: '{linkedIssueId}'. Expected 'proj-{{number}}-issue-{{number}}'.");

            var issue = await apiClient.GetIssueAsync(issueProjId, issueNumber, ct);
            if (issue is null)
                return OperationResult.Warning($"Linked issue '{linkedIssueId}' not found.");

            request.LinkedIssueId = issue.Id;
        }

        if (linkedFeatureRequestId is not null)
        {
            if (!IdParser.TryParseFeatureRequestId(linkedFeatureRequestId, out var frProjId, out var frNumber))
                return OperationResult.Error($"Invalid linked feature request ID format: '{linkedFeatureRequestId}'. Expected 'proj-{{number}}-fr-{{number}}'.");

            var fr = await apiClient.GetFeatureRequestAsync(frProjId, frNumber, ct);
            if (fr is null)
                return OperationResult.Warning($"Linked feature request '{linkedFeatureRequestId}' not found.");

            request.LinkedFeatureRequestId = fr.Id;
        }

        var created = await apiClient.CreateWorkPackageAsync(projId, request, ct);
        return OperationResult.Success(created.WorkPackageId, $"Work package '{name}' created.");
    }

    private async Task<string> UpdateExistingWorkPackage(
        long projId, string workPackageId, string? name, string? description, WorkPackageType? type,
        Priority? priority, string? plan, int? estimatedComplexity, string? estimationRationale,
        CompletionState? state, string? linkedIssueId, string? linkedFeatureRequestId, List<FileReferenceInput>? attachments, CancellationToken ct)
    {
        if (!IdParser.TryParseWorkPackageId(workPackageId, out var parsedProjId, out var wpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{workPackageId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        if (parsedProjId != projId)
            return OperationResult.Error($"Work package ID '{workPackageId}' does not belong to project 'proj-{projId}'.");

        var request = new UpdateWorkPackageRequest
        {
            Name = name,
            Description = description,
            Type = type,
            Priority = priority,
            Plan = plan,
            EstimatedComplexity = estimatedComplexity,
            EstimationRationale = estimationRationale,
            State = state,
            Attachments = attachments is not null ? McpInputParser.MapFileReferences(attachments) : null
        };

        if (linkedIssueId is not null)
        {
            if (!IdParser.TryParseIssueId(linkedIssueId, out var issueProjId, out var issueNumber))
                return OperationResult.Error($"Invalid linked issue ID format: '{linkedIssueId}'. Expected 'proj-{{number}}-issue-{{number}}'.");

            var issue = await apiClient.GetIssueAsync(issueProjId, issueNumber, ct);
            if (issue is null)
                return OperationResult.Warning($"Linked issue '{linkedIssueId}' not found.");

            request.LinkedIssueId = issue.Id;
        }

        if (linkedFeatureRequestId is not null)
        {
            if (!IdParser.TryParseFeatureRequestId(linkedFeatureRequestId, out var frProjId, out var frNumber))
                return OperationResult.Error($"Invalid linked feature request ID format: '{linkedFeatureRequestId}'. Expected 'proj-{{number}}-fr-{{number}}'.");

            var fr = await apiClient.GetFeatureRequestAsync(frProjId, frNumber, ct);
            if (fr is null)
                return OperationResult.Warning($"Linked feature request '{linkedFeatureRequestId}' not found.");

            request.LinkedFeatureRequestId = fr.Id;
        }

        var updated = await apiClient.UpdateWorkPackageAsync(projId, wpNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Work package '{workPackageId}' not found.");

        return OperationResult.Success(workPackageId, $"Work package '{workPackageId}' updated.",
            stateChanges: updated.StateChanges);
    }

    // ── Mapping helpers ──

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

    [McpServerTool(Name = "delete_work_package",
        Title = "Delete Work Package", Destructive = true, OpenWorld = false)]
    [Description(
        "Permanently deletes a work package and all its phases and tasks. " +
        "Linked issues and FRs will have their WP link cleared (not deleted). " +
        "This action cannot be undone.")]
    public async Task<string> DeleteWorkPackage(
        [Description("Work package ID (e.g. 'proj-1-wp-2').")] string workPackageId,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseWorkPackageId(workPackageId, out var projId, out var wpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{workPackageId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        try
        {
            var deleted = await apiClient.DeleteWorkPackageAsync(projId, wpNumber, ct);
            return deleted
                ? OperationResult.Success(workPackageId, $"Deleted work package '{workPackageId}' and all its phases and tasks.")
                : OperationResult.Warning($"Work package '{workPackageId}' not found.");
        }
        catch (HttpRequestException ex)
        {
            return OperationResult.Error($"API error: {ex.Message}");
        }
    }

    private static DependencyItem MapWpDependency(DependencyResponse dep) => new()
    {
        EntityId = dep.WorkPackageId, Name = dep.Name, State = dep.State, Reason = dep.Reason
    };

    private static DependencyItem MapTaskDependency(TaskDependencyResponse dep) => new()
    {
        EntityId = dep.TaskId, Name = dep.Name, State = dep.State, Reason = dep.Reason
    };
}
