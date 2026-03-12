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
        "Does NOT include phase/task trees, dependencies, or linked issues/FRs. " +
        "For full WP tree (phases, tasks, dependencies), use get_work_package_details. " +
        "For WP counts by category, use get_project_status.")]
    public async Task<string> GetWorkPackages(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Filter by state category. Omit for all work packages.")] StateFilterCategory? stateFilter = null,
        CancellationToken ct = default)
    {
        try
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
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to get work packages: {ex.Message}");
        }
    }

    // ── 2. get_work_package_details ──

    [McpServerTool(Name = "get_work_package_details", ReadOnly = true,
        Title = "Get Work Package Details", OpenWorld = false)]
    [Description(
        "Returns the full work package tree: phases with acceptance criteria, tasks with target files " +
        "and implementation notes, WP-level and task-level dependencies, " +
        "linked issue IDs (linkedIssueIds) and linked FR IDs (linkedFeatureRequestIds). " +
        "Use get_work_packages for a compact list first, then drill into specific WPs with this tool.")]
    public async Task<string> GetWorkPackageDetails(
        [Description("Work package ID (e.g. 'proj-1-wp-2').")] string workPackageId,
        CancellationToken ct = default)
    {
        try
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
                LinkedIssueIds = McpInputParser.NullIfEmpty(wp.LinkedIssueIds),
                LinkedFeatureRequestIds = McpInputParser.NullIfEmpty(wp.LinkedFeatureRequestIds),
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
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to get work package details: {ex.Message}");
        }
    }

    // ── 3. create_or_update_work_package ──

    [McpServerTool(Name = "create_or_update_work_package",
        Title = "Create or Update Work Package", Destructive = false, OpenWorld = false)]
    [Description(
        "Creates a new work package or updates an existing one. " +
        "Returns OperationResult with the WP ID (e.g. 'proj-1-wp-3') and any cascade state changes. " +
        "To create: provide projectId and required fields (name, description). " +
        "To update: provide projectId and workPackageId, plus any fields to change (PATCH semantics: null = keep current). " +
        "Does NOT create phases or tasks — use scaffold_work_package for that, or create_or_update_phase after.")]
    public async Task<string> CreateOrUpdateWorkPackage(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Work package ID (e.g. 'proj-1-wp-2'). Omit to create a new work package.")] string? workPackageId = null,
        [Description("Work package name/title.")] string? name = null,
        [Description("Detailed description of the work package.")] string? description = null,
        [Description("Work package type. Default: Feature.")] WorkPackageType? type = null,
        [Description("Priority level. Default: Medium.")] Priority? priority = null,
        [Description("Implementation plan (supports markdown).")] string? plan = null,
        [Description("Estimated complexity on a 1-10 scale (e.g. 3 for simple, 7 for complex).")] int? estimatedComplexity = null,
        [Description("Rationale for the complexity estimation.")] string? estimationRationale = null,
        [Description("Completion state (e.g. NotStarted, Implementing, Completed). Omit to keep current.")] CompletionState? state = null,
        [Description("Linked issue IDs (e.g. ['proj-1-issue-3']). Provide to set/replace all linked issues. Omit to keep current.")] List<string>? linkedIssueIds = null,
        [Description("Linked feature request IDs (e.g. ['proj-1-fr-1']). Provide to set/replace all linked FRs. Omit to keep current.")] List<string>? linkedFeatureRequestIds = null,
        [Description("File attachments.")] List<FileReferenceInput>? attachments = null,
        CancellationToken ct = default)
    {
        try
        {
            if (!IdParser.TryParseProjectId(projectId, out var projId))
                return OperationResult.Error($"Invalid project ID format: '{projectId}'. Expected 'proj-{{number}}'.");

            if (workPackageId is not null)
                return await UpdateExistingWorkPackage(projId, workPackageId, name, description, type,
                    priority, plan, estimatedComplexity, estimationRationale, state, linkedIssueIds, linkedFeatureRequestIds, attachments, ct);

            return await CreateNewWorkPackage(projId, name, description, type, priority, plan,
                estimatedComplexity, estimationRationale, state, linkedIssueIds, linkedFeatureRequestIds, attachments, ct);
        }
        catch (Exception ex)
        {
            return OperationResult.Error($"Failed to create/update work package: {ex.Message}");
        }
    }

    // ── 4. scaffold_work_package ──

    [McpServerTool(Name = "scaffold_work_package",
        Title = "Scaffold Work Package", Destructive = false, OpenWorld = false)]
    [Description(
        "Creates a complete work package with phases, tasks, acceptance criteria, and dependencies in ONE call. " +
        "This is the most efficient way to create structured work. " +
        "Tasks require name + description; all other fields are optional. " +
        "Task dependencies use 0-based indices within the same phase's task array (dependsOnTaskIndices) — " +
        "does NOT support cross-phase task dependencies. " +
        "Supports WP-level blockers via blockedByWorkPackageIds and linked issues/FRs via linkedIssueIds/linkedFeatureRequestIds. " +
        "Returns a ScaffoldOperationResult with the WP ID plus an ID map of all created phases and tasks. " +
        "Does NOT update existing work packages — use create_or_update_work_package for updates.")]
    public async Task<string> ScaffoldWorkPackage(
        [Description("Project ID (e.g. 'proj-1').")] string projectId,
        [Description("Work package name/title.")] string name,
        [Description("Detailed description of the work package.")] string description,
        [Description("Phases with optional tasks, acceptance criteria, and task dependencies.")] List<ScaffoldPhaseInput> phases,
        [Description("Work package type. Default: Feature.")] WorkPackageType? type = null,
        [Description("Priority level. Default: Medium.")] Priority? priority = null,
        [Description("Implementation plan (supports markdown).")] string? plan = null,
        [Description("Estimated complexity on a 1-10 scale (e.g. 3 for simple, 7 for complex).")] int? estimatedComplexity = null,
        [Description("Rationale for the complexity estimation.")] string? estimationRationale = null,
        [Description("Completion state (e.g. NotStarted, Implementing). Default: NotStarted.")] CompletionState? state = null,
        [Description("Linked issue IDs (e.g. ['proj-1-issue-3']).")] List<string>? linkedIssueIds = null,
        [Description("Linked feature request IDs (e.g. ['proj-1-fr-1']).")] List<string>? linkedFeatureRequestIds = null,
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

            var resolvedIssueIds = await ResolveIssueIdsAsync(linkedIssueIds, ct);
            if (resolvedIssueIds is null)
                return OperationResult.Error("One or more linked issue IDs are invalid. Expected 'proj-{number}-issue-{number}'.");
            if (resolvedIssueIds.Count > 0)
                request.LinkedIssueIds = resolvedIssueIds;

            var resolvedFrIds = await ResolveFrIdsAsync(linkedFeatureRequestIds, ct);
            if (resolvedFrIds is null)
                return OperationResult.Error("One or more linked FR IDs are invalid. Expected 'proj-{number}-fr-{number}'.");
            if (resolvedFrIds.Count > 0)
                request.LinkedFeatureRequestIds = resolvedFrIds;

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
        List<string>? linkedIssueIds, List<string>? linkedFeatureRequestIds, List<FileReferenceInput>? attachments, CancellationToken ct)
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

        var resolvedIssueIds = await ResolveIssueIdsAsync(linkedIssueIds, ct);
        if (resolvedIssueIds is null)
            return OperationResult.Error("One or more linked issue IDs are invalid. Expected 'proj-{number}-issue-{number}'.");
        if (resolvedIssueIds.Count > 0)
            request.LinkedIssueIds = resolvedIssueIds;

        var resolvedFrIds = await ResolveFrIdsAsync(linkedFeatureRequestIds, ct);
        if (resolvedFrIds is null)
            return OperationResult.Error("One or more linked FR IDs are invalid. Expected 'proj-{number}-fr-{number}'.");
        if (resolvedFrIds.Count > 0)
            request.LinkedFeatureRequestIds = resolvedFrIds;

        var created = await apiClient.CreateWorkPackageAsync(projId, request, ct);
        return OperationResult.Success(created.WorkPackageId, $"Work package '{name}' created.");
    }

    private async Task<string> UpdateExistingWorkPackage(
        long projId, string workPackageId, string? name, string? description, WorkPackageType? type,
        Priority? priority, string? plan, int? estimatedComplexity, string? estimationRationale,
        CompletionState? state, List<string>? linkedIssueIds, List<string>? linkedFeatureRequestIds, List<FileReferenceInput>? attachments, CancellationToken ct)
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

        var resolvedIssueIds = await ResolveIssueIdsAsync(linkedIssueIds, ct);
        if (resolvedIssueIds is null)
            return OperationResult.Error("One or more linked issue IDs are invalid. Expected 'proj-{number}-issue-{number}'.");
        if (resolvedIssueIds.Count > 0)
            request.LinkedIssueIds = resolvedIssueIds;
        else if (linkedIssueIds is not null)
            request.LinkedIssueIds = []; // explicitly clear

        var resolvedFrIds = await ResolveFrIdsAsync(linkedFeatureRequestIds, ct);
        if (resolvedFrIds is null)
            return OperationResult.Error("One or more linked FR IDs are invalid. Expected 'proj-{number}-fr-{number}'.");
        if (resolvedFrIds.Count > 0)
            request.LinkedFeatureRequestIds = resolvedFrIds;
        else if (linkedFeatureRequestIds is not null)
            request.LinkedFeatureRequestIds = []; // explicitly clear

        var updated = await apiClient.UpdateWorkPackageAsync(projId, wpNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Work package '{workPackageId}' not found.");

        return OperationResult.Success(workPackageId, $"Work package '{workPackageId}' updated.",
            stateChanges: updated.StateChanges);
    }

    /// <summary>Resolves human-readable issue IDs to DB IDs. Returns null on invalid format.</summary>
    private async Task<List<long>> ResolveIssueIdsAsync(List<string>? humanIds, CancellationToken ct)
    {
        if (humanIds is not { Count: > 0 }) return [];

        var result = new List<long>();
        foreach (var id in humanIds)
        {
            if (!IdParser.TryParseIssueId(id, out var projId, out var number))
                return null!;
            var issue = await apiClient.GetIssueAsync(projId, number, ct);
            if (issue is null) return null!;
            result.Add(issue.Id);
        }
        return result;
    }

    /// <summary>Resolves human-readable FR IDs to DB IDs. Returns null on invalid format.</summary>
    private async Task<List<long>> ResolveFrIdsAsync(List<string>? humanIds, CancellationToken ct)
    {
        if (humanIds is not { Count: > 0 }) return [];

        var result = new List<long>();
        foreach (var id in humanIds)
        {
            if (!IdParser.TryParseFeatureRequestId(id, out var projId, out var number))
                return null!;
            var fr = await apiClient.GetFeatureRequestAsync(projId, number, ct);
            if (fr is null) return null!;
            result.Add(fr.Id);
        }
        return result;
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

    private static DependencyItem MapWpDependency(DependencyResponse dep) => new()
    {
        EntityId = dep.WorkPackageId, Name = dep.Name, State = dep.State, Reason = dep.Reason
    };

    private static DependencyItem MapTaskDependency(TaskDependencyResponse dep) => new()
    {
        EntityId = dep.TaskId, Name = dep.Name, State = dep.State, Reason = dep.Reason
    };
}
