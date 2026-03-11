using System.ComponentModel;
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
public sealed class TaskTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "create_or_update_task",
        Title = "Create or Update Task", Destructive = false, OpenWorld = false)]
    [Description(
        "Creates a new task or updates an existing one. " +
        "To create: provide phaseId with name and description. " +
        "To update: provide taskId plus fields to change. " +
        "For creating multiple tasks at once, use create_or_update_phase with the tasks parameter or scaffold_work_package instead.")]
    public async Task<string> CreateOrUpdateTask(
        [Description("Phase ID (e.g. 'proj-1-wp-2-phase-1'). Required for task creation.")] string? phaseId = null,
        [Description("Task ID (e.g. 'proj-1-wp-2-task-5'). Provide to update an existing task.")] string? taskId = null,
        [Description("Task name.")] string? name = null,
        [Description("Task description.")] string? description = null,
        [Description("Sort order for display ordering.")] int? sortOrder = null,
        [Description("Implementation notes (supports markdown).")] string? implementationNotes = null,
        [Description("Completion state (e.g. NotStarted, Implementing, Completed). Omit to keep current.")] CompletionState? state = null,
        [Description("Target files for this task.")] List<FileReferenceInput>? targetFiles = null,
        [Description("File attachments.")] List<FileReferenceInput>? attachments = null,
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

    [McpServerTool(Name = "batch_update_task_states",
        Title = "Batch Update Task States", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Updates the state of multiple tasks in a single work package in one operation. " +
        "Cascades (auto-unblock, phase/WP auto-complete) run once after all transitions, " +
        "returning a consolidated list of state changes. " +
        "For updating individual task fields beyond state, use create_or_update_task instead.")]
    public async Task<string> BatchUpdateTaskStates(
        [Description("Work package ID (e.g. 'proj-1-wp-2').")] string workPackageId,
        [Description("Task state updates to apply.")] List<BatchTaskStateInput> tasks,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseWorkPackageId(workPackageId, out var projId, out var wpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{workPackageId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        if (tasks is { Count: 0 })
            return OperationResult.Error("'tasks' array must contain at least one entry.");

        var taskUpdates = new List<TaskStateUpdate>();
        foreach (var item in tasks)
        {
            if (!IdParser.TryParseTaskId(item.TaskId, out var taskProjId, out var taskWpNumber, out var taskNumber))
                return OperationResult.Error($"Invalid task ID format: '{item.TaskId}'. Expected 'proj-{{number}}-wp-{{number}}-task-{{number}}'.");

            if (taskProjId != projId || taskWpNumber != wpNumber)
                return OperationResult.Error($"Task '{item.TaskId}' does not belong to work package '{workPackageId}'.");

            taskUpdates.Add(new TaskStateUpdate
            {
                TaskNumber = taskNumber,
                State = item.State
            });
        }

        var request = new BatchUpdateTaskStatesRequest { Tasks = taskUpdates };

        try
        {
            var response = await apiClient.BatchUpdateTaskStatesAsync(projId, wpNumber, request, ct);
            if (response is null)
                return OperationResult.Warning($"Work package '{workPackageId}' not found.");

            var message = $"{response.UpdatedCount} task(s) updated in '{workPackageId}'.";
            return OperationResult.Success(workPackageId, message, stateChanges: response.StateChanges);
        }
        catch (HttpRequestException ex)
        {
            return OperationResult.Error($"Batch update failed: {ex.Message}");
        }
    }

    [McpServerTool(Name = "manage_task_dependency",
        Title = "Manage Task Dependency", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Adds or removes a dependency between tasks within the same project. " +
        "When adding: if the blocker is non-terminal, the dependent auto-transitions to Blocked. " +
        "When the blocker completes, dependents auto-unblock. " +
        "Returns stateChanges showing any automatic state transitions.")]
    public async Task<string> ManageTaskDependency(
        [Description("Dependent task ID (e.g. 'proj-1-wp-2-task-3').")] string taskId,
        [Description("Blocker task ID (e.g. 'proj-1-wp-2-task-1').")] string dependsOnTaskId,
        [Description("Whether to add or remove the dependency.")] DependencyAction action,
        [Description("Reason for the dependency.")] string? reason = null,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseTaskId(taskId, out var projId, out var wpNumber, out var taskNumber))
            return OperationResult.Error($"Invalid task ID format: '{taskId}'. Expected 'proj-{{number}}-wp-{{number}}-task-{{number}}'.");

        if (!IdParser.TryParseTaskId(dependsOnTaskId, out var depProjId, out var depWpNumber, out var depTaskNumber))
            return OperationResult.Error($"Invalid task ID format: '{dependsOnTaskId}'. Expected 'proj-{{number}}-wp-{{number}}-task-{{number}}'.");

        var dependsOnWp = await apiClient.GetWorkPackageAsync(depProjId, depWpNumber, ct);
        if (dependsOnWp is null)
            return OperationResult.Warning($"Work package containing depends-on task '{dependsOnTaskId}' not found.");

        var dependsOnTask = dependsOnWp.Phases
            .SelectMany(p => p.Tasks)
            .FirstOrDefault(t => t.TaskNumber == depTaskNumber);
        if (dependsOnTask is null)
            return OperationResult.Warning($"Depends-on task '{dependsOnTaskId}' not found.");

        switch (action)
        {
            case DependencyAction.Add:
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

            case DependencyAction.Remove:
                var removed = await apiClient.RemoveTaskDependencyAsync(projId, wpNumber, taskNumber, dependsOnTask.Id, ct);
                return removed
                    ? OperationResult.Success(taskId, $"Dependency removed: '{taskId}' is no longer blocked by '{dependsOnTaskId}'.")
                    : OperationResult.Warning($"Dependency between '{taskId}' and '{dependsOnTaskId}' not found.");

            default:
                return OperationResult.Error($"Invalid action: '{action}'.");
        }
    }

    private async Task<string> CreateNewTask(
        string phaseId, string? name, string? description, int? sortOrder,
        string? implementationNotes, CompletionState? state, List<FileReferenceInput>? targetFiles,
        List<FileReferenceInput>? attachments, CancellationToken ct)
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
            SortOrder = sortOrder,
            ImplementationNotes = implementationNotes,
            State = state ?? CompletionState.NotStarted,
            TargetFiles = McpInputParser.MapFileReferences(targetFiles),
            Attachments = McpInputParser.MapFileReferences(attachments)
        };

        var created = await apiClient.CreateTaskAsync(projId, wpNumber, phaseNumber, request, ct);
        return OperationResult.Success(created.TaskId, $"Task '{name}' created.");
    }

    private async Task<string> UpdateExistingTask(
        string taskId, string? name, string? description, int? sortOrder,
        string? implementationNotes, CompletionState? state, List<FileReferenceInput>? targetFiles,
        List<FileReferenceInput>? attachments, CancellationToken ct)
    {
        if (!IdParser.TryParseTaskId(taskId, out var projId, out var wpNumber, out var taskNumber))
            return OperationResult.Error($"Invalid task ID format: '{taskId}'. Expected 'proj-{{number}}-wp-{{number}}-task-{{number}}'.");

        var request = new UpdateTaskRequest
        {
            Name = name,
            Description = description,
            SortOrder = sortOrder,
            ImplementationNotes = implementationNotes,
            State = state,
            TargetFiles = targetFiles is not null ? McpInputParser.MapFileReferences(targetFiles) : null,
            Attachments = attachments is not null ? McpInputParser.MapFileReferences(attachments) : null
        };

        var updated = await apiClient.UpdateTaskAsync(projId, wpNumber, taskNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Task '{taskId}' not found.");

        return OperationResult.Success(taskId, $"Task '{taskId}' updated.",
            stateChanges: updated.StateChanges);
    }

    [McpServerTool(Name = "delete_task",
        Title = "Delete Task", Destructive = true, OpenWorld = false)]
    [Description(
        "Permanently deletes a task. " +
        "This action cannot be undone.")]
    public async Task<string> DeleteTask(
        [Description("Task ID (e.g. 'proj-1-wp-2-task-3').")] string taskId,
        CancellationToken ct = default)
    {
        if (!IdParser.TryParseTaskId(taskId, out var projId, out var wpNumber, out var taskNumber))
            return OperationResult.Error($"Invalid task ID format: '{taskId}'. Expected 'proj-{{number}}-wp-{{number}}-task-{{number}}'.");

        try
        {
            var deleted = await apiClient.DeleteTaskAsync(projId, wpNumber, taskNumber, ct);
            return deleted
                ? OperationResult.Success(taskId, $"Deleted task '{taskId}'.")
                : OperationResult.Warning($"Task '{taskId}' not found.");
        }
        catch (HttpRequestException ex)
        {
            return OperationResult.Error($"API error: {ex.Message}");
        }
    }
}
