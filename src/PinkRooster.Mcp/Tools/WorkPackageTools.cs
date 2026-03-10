using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class WorkPackageTools(PinkRoosterApiClient apiClient)
{
    private static readonly HashSet<string> TerminalStateStrings =
        ["Completed", "Cancelled", "Replaced"];

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
            CompletedTaskCount = wp.Phases.Sum(p => p.Tasks.Count(t => IsTerminalState(t.State))),
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
            Attachments = NullIfEmpty(wp.Attachments),
            Phases = wp.Phases.Select(MapPhaseDetail).ToList(),
            BlockedBy = NullIfEmpty(wp.BlockedBy.Select(MapWpDependency).ToList()),
            Blocking = NullIfEmpty(wp.Blocking.Select(MapWpDependency).ToList()),
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

    // ── 5. create_or_update_phase ──

    [McpServerTool(Name = "create_or_update_phase")]
    [Description(
        "Creates a new phase or updates an existing one. Can include tasks for batch creation/update. " +
        "To create: provide workPackageId and name. To update: provide phaseId plus fields to change.")]
    public async Task<string> CreateOrUpdatePhase(
        [Description("Work package ID in 'proj-{number}-wp-{number}' format.")] string workPackageId,
        [Description("Phase ID in 'proj-{number}-wp-{number}-phase-{number}' format. Omit to create a new phase.")] string? phaseId = null,
        [Description("Phase name.")] string? name = null,
        [Description("Phase description.")] string? description = null,
        [Description("Sort order (integer).")] string? sortOrder = null,
        [Description("State: NotStarted, Designing, Implementing, Testing, InReview, Completed, Cancelled, Blocked, Replaced")] string? state = null,
        [Description("Acceptance criteria as JSON array: [{\"name\":\"...\",\"description\":\"...\",\"verificationMethod\":\"Manual|Automated|CodeReview\"}]")] string? acceptanceCriteria = null,
        [Description("Tasks as JSON array. For create: [{\"name\":\"...\",\"description\":\"...\"}]. For update: [{\"taskNumber\":1,\"name\":\"...\"}]")] string? tasks = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseWorkPackageId(workPackageId, out var projId, out var wpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{workPackageId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        if (phaseId is not null)
            return await UpdateExistingPhase(projId, wpNumber, phaseId, name, description, sortOrder,
                state, acceptanceCriteria, tasks, ct);

        return await CreateNewPhase(projId, wpNumber, name, description, sortOrder,
            acceptanceCriteria, tasks, ct);
    }

    // ── 6. create_or_update_task ──

    [McpServerTool(Name = "create_or_update_task")]
    [Description(
        "Creates a new task or updates an existing one. " +
        "To create: provide phaseId with name and description. " +
        "To update: provide taskId plus fields to change.")]
    public async Task<string> CreateOrUpdateTask(
        [Description("Phase ID in 'proj-{number}-wp-{number}-phase-{number}' format. Required for task creation.")] string? phaseId = null,
        [Description("Task ID in 'proj-{number}-wp-{number}-task-{number}' format. Provide to update an existing task.")] string? taskId = null,
        [Description("Task name.")] string? name = null,
        [Description("Task description.")] string? description = null,
        [Description("Sort order (integer).")] string? sortOrder = null,
        [Description("Implementation notes.")] string? implementationNotes = null,
        [Description("State: NotStarted, Designing, Implementing, Testing, InReview, Completed, Cancelled, Blocked, Replaced")] string? state = null,
        [Description("Target files as JSON array: [{\"fileName\":\"...\",\"relativePath\":\"...\",\"description\":\"...\"}]")] string? targetFiles = null,
        [Description("File attachments as JSON array: [{\"fileName\":\"...\",\"relativePath\":\"...\",\"description\":\"...\"}]")] string? attachments = null,
        CancellationToken ct = default)
    {
        if (taskId is not null)
            return await UpdateExistingTask(taskId, name, description, sortOrder,
                implementationNotes, state, targetFiles, attachments, ct);

        if (phaseId is null)
            return OperationResult.Error("Either 'phaseId' (for create) or 'taskId' (for update) must be provided.");

        return await CreateNewTask(phaseId, name, description, sortOrder,
            implementationNotes, state, targetFiles, attachments, ct);
    }

    // ── 7. manage_task_dependency ──

    [McpServerTool(Name = "manage_task_dependency")]
    [Description("Adds or removes a dependency between tasks. The dependent task is blocked by the depends-on task.")]
    public async Task<string> ManageTaskDependency(
        [Description("Dependent task ID in 'proj-{number}-wp-{number}-task-{number}' format.")] string taskId,
        [Description("Depends-on task ID in 'proj-{number}-wp-{number}-task-{number}' format.")] string dependsOnTaskId,
        [Description("Action: 'add' or 'remove'.")] string action,
        [Description("Reason for the dependency.")] string? reason = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseTaskId(taskId, out var projId, out var wpNumber, out var taskNumber))
            return OperationResult.Error($"Invalid task ID format: '{taskId}'. Expected 'proj-{{number}}-wp-{{number}}-task-{{number}}'.");

        if (!IdParser.TryParseTaskId(dependsOnTaskId, out var depProjId, out var depWpNumber, out var depTaskNumber))
            return OperationResult.Error($"Invalid task ID format: '{dependsOnTaskId}'. Expected 'proj-{{number}}-wp-{{number}}-task-{{number}}'.");

        // Look up the depends-on task's internal ID via the work package tree
        var dependsOnWp = await apiClient.GetWorkPackageAsync(depProjId, depWpNumber, ct);
        if (dependsOnWp is null)
            return OperationResult.Warning($"Work package containing depends-on task '{dependsOnTaskId}' not found.");

        var dependsOnTask = dependsOnWp.Phases
            .SelectMany(p => p.Tasks)
            .FirstOrDefault(t => t.TaskNumber == depTaskNumber);
        if (dependsOnTask is null)
            return OperationResult.Warning($"Depends-on task '{dependsOnTaskId}' not found.");

        switch (action.ToLowerInvariant())
        {
            case "add":
                var request = new ManageDependencyRequest
                {
                    DependsOnId = dependsOnTask.Id,
                    Reason = reason
                };
                TaskDependencyResponse taskDepResponse;
                try
                {
                    taskDepResponse = await apiClient.AddTaskDependencyAsync(projId, wpNumber, taskNumber, request, ct);
                }
                catch (HttpRequestException ex)
                {
                    return OperationResult.Error($"Failed to add dependency: {ex.Message}");
                }
                return OperationResult.Success(taskId,
                    $"Dependency added: '{taskId}' is now blocked by '{dependsOnTaskId}'.",
                    stateChanges: taskDepResponse.StateChanges);

            case "remove":
                var removed = await apiClient.RemoveTaskDependencyAsync(projId, wpNumber, taskNumber, dependsOnTask.Id, ct);
                return removed
                    ? OperationResult.Success(taskId, $"Dependency removed: '{taskId}' is no longer blocked by '{dependsOnTaskId}'.")
                    : OperationResult.Warning($"Dependency between '{taskId}' and '{dependsOnTaskId}' not found.");

            default:
                return OperationResult.Error($"Invalid action: '{action}'. Expected 'add' or 'remove'.");
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
            Type = ParseEnumOrDefault(type, WorkPackageType.Feature),
            Priority = ParseEnumOrDefault(priority, Priority.Medium),
            Plan = plan,
            EstimatedComplexity = ParseInt(estimatedComplexity),
            EstimationRationale = estimationRationale,
            State = ParseEnumOrDefault(state, CompletionState.NotStarted),
            Attachments = ParseAttachments(attachments)
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
            Type = type is not null ? ParseEnum<WorkPackageType>(type) : null,
            Priority = priority is not null ? ParseEnum<Priority>(priority) : null,
            Plan = plan,
            EstimatedComplexity = ParseInt(estimatedComplexity),
            EstimationRationale = estimationRationale,
            State = state is not null ? ParseEnum<CompletionState>(state) : null,
            Attachments = attachments is not null ? ParseAttachments(attachments) : null
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

    // ── Private helpers: Phase create/update ──

    private async Task<string> CreateNewPhase(
        long projId, int wpNumber, string? name, string? description, string? sortOrder,
        string? acceptanceCriteria, string? tasks, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Error("'name' is required when creating a phase.");

        var request = new CreatePhaseRequest
        {
            Name = name,
            Description = description,
            SortOrder = ParseInt(sortOrder),
            AcceptanceCriteria = ParseAcceptanceCriteria(acceptanceCriteria),
            Tasks = ParseCreateTasks(tasks)
        };

        var created = await apiClient.CreatePhaseAsync(projId, wpNumber, request, ct);
        return OperationResult.Success(created.PhaseId, $"Phase '{name}' created.");
    }

    private async Task<string> UpdateExistingPhase(
        long projId, int wpNumber, string phaseId, string? name, string? description,
        string? sortOrder, string? state, string? acceptanceCriteria, string? tasks,
        CancellationToken ct)
    {
        if (!IdParser.TryParsePhaseId(phaseId, out var parsedProjId, out var parsedWpNumber, out var phaseNumber))
            return OperationResult.Error($"Invalid phase ID format: '{phaseId}'. Expected 'proj-{{number}}-wp-{{number}}-phase-{{number}}'.");

        if (parsedProjId != projId || parsedWpNumber != wpNumber)
            return OperationResult.Error($"Phase ID '{phaseId}' does not belong to work package 'proj-{projId}-wp-{wpNumber}'.");

        var request = new UpdatePhaseRequest
        {
            Name = name,
            Description = description,
            SortOrder = ParseInt(sortOrder),
            State = state is not null ? ParseEnum<CompletionState>(state) : null,
            AcceptanceCriteria = ParseAcceptanceCriteria(acceptanceCriteria),
            Tasks = ParseUpsertTasks(tasks)
        };

        var updated = await apiClient.UpdatePhaseAsync(projId, wpNumber, phaseNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Phase '{phaseId}' not found.");

        return OperationResult.Success(phaseId, $"Phase '{phaseId}' updated.",
            stateChanges: updated.StateChanges);
    }

    // ── Private helpers: Task create/update ──

    private async Task<string> CreateNewTask(
        string phaseId, string? name, string? description, string? sortOrder,
        string? implementationNotes, string? state, string? targetFiles, string? attachments,
        CancellationToken ct)
    {
        if (!IdParser.TryParsePhaseId(phaseId, out var projId, out var wpNumber, out var phaseNumber))
            return OperationResult.Error($"Invalid phase ID format: '{phaseId}'. Expected 'proj-{{number}}-wp-{{number}}-phase-{{number}}'.");

        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Error("'name' is required when creating a task.");
        if (string.IsNullOrWhiteSpace(description))
            return OperationResult.Error("'description' is required when creating a task.");

        var request = new CreateTaskRequest
        {
            Name = name,
            Description = description,
            SortOrder = ParseInt(sortOrder),
            ImplementationNotes = implementationNotes,
            State = ParseEnumOrDefault(state, CompletionState.NotStarted),
            TargetFiles = ParseAttachments(targetFiles),
            Attachments = ParseAttachments(attachments)
        };

        var created = await apiClient.CreateTaskAsync(projId, wpNumber, phaseNumber, request, ct);
        return OperationResult.Success(created.TaskId, $"Task '{name}' created.");
    }

    private async Task<string> UpdateExistingTask(
        string taskId, string? name, string? description, string? sortOrder,
        string? implementationNotes, string? state, string? targetFiles, string? attachments,
        CancellationToken ct)
    {
        if (!IdParser.TryParseTaskId(taskId, out var projId, out var wpNumber, out var taskNumber))
            return OperationResult.Error($"Invalid task ID format: '{taskId}'. Expected 'proj-{{number}}-wp-{{number}}-task-{{number}}'.");

        var request = new UpdateTaskRequest
        {
            Name = name,
            Description = description,
            SortOrder = ParseInt(sortOrder),
            ImplementationNotes = implementationNotes,
            State = state is not null ? ParseEnum<CompletionState>(state) : null,
            TargetFiles = targetFiles is not null ? ParseAttachments(targetFiles) : null,
            Attachments = attachments is not null ? ParseAttachments(attachments) : null
        };

        var updated = await apiClient.UpdateTaskAsync(projId, wpNumber, taskNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Task '{taskId}' not found.");

        return OperationResult.Success(taskId, $"Task '{taskId}' updated.",
            stateChanges: updated.StateChanges);
    }

    // ── Mapping helpers ──

    private static PhaseDetailItem MapPhaseDetail(PhaseResponse phase)
    {
        return new PhaseDetailItem
        {
            PhaseId = phase.PhaseId,
            Name = phase.Name,
            Description = phase.Description,
            SortOrder = phase.SortOrder,
            State = phase.State,
            AcceptanceCriteria = NullIfEmpty(phase.AcceptanceCriteria.Select(ac => new AcceptanceCriterionItem
            {
                Name = ac.Name,
                Description = ac.Description,
                VerificationMethod = ac.VerificationMethod.ToString(),
                VerificationResult = ac.VerificationResult,
                VerifiedAt = ac.VerifiedAt
            }).ToList()),
            Tasks = phase.Tasks.Select(MapTaskDetail).ToList()
        };
    }

    private static TaskDetailItem MapTaskDetail(TaskResponse task)
    {
        return new TaskDetailItem
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
            TargetFiles = NullIfEmpty(task.TargetFiles),
            Attachments = NullIfEmpty(task.Attachments),
            BlockedBy = NullIfEmpty(task.BlockedBy.Select(MapTaskDependency).ToList()),
            Blocking = NullIfEmpty(task.Blocking.Select(MapTaskDependency).ToList())
        };
    }

    private static DependencyItem MapWpDependency(DependencyResponse dep)
    {
        return new DependencyItem
        {
            EntityId = dep.WorkPackageId,
            Name = dep.Name,
            State = dep.State,
            Reason = dep.Reason
        };
    }

    private static DependencyItem MapTaskDependency(TaskDependencyResponse dep)
    {
        return new DependencyItem
        {
            EntityId = dep.TaskId,
            Name = dep.Name,
            State = dep.State,
            Reason = dep.Reason
        };
    }

    // ── Parsing helpers ──

    private static bool IsTerminalState(string state) =>
        TerminalStateStrings.Contains(state);

    private static List<T>? NullIfEmpty<T>(List<T> list) =>
        list.Count == 0 ? null : list;

    private static TEnum ParseEnumOrDefault<TEnum>(string? value, TEnum defaultValue) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : defaultValue;
    }

    private static TEnum? ParseEnum<TEnum>(string value) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : null;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static List<FileReferenceDto>? ParseAttachments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<FileReferenceDto>>(json, JsonDefaults.Indented);
        }
        catch
        {
            return null;
        }
    }

    private static List<AcceptanceCriterionDto>? ParseAcceptanceCriteria(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<AcceptanceCriterionDto>>(json, JsonDefaults.Indented);
        }
        catch
        {
            return null;
        }
    }

    private static List<CreateTaskRequest>? ParseCreateTasks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<CreateTaskRequest>>(json, JsonDefaults.Indented);
        }
        catch
        {
            return null;
        }
    }

    private static List<UpsertTaskInPhaseDto>? ParseUpsertTasks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<UpsertTaskInPhaseDto>>(json, JsonDefaults.Indented);
        }
        catch
        {
            return null;
        }
    }
}
