using System.ComponentModel;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Helpers;
using PinkRooster.Mcp.Inputs;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.Enums;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class TaskTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "create_or_update_task",
        Title = "Create or Update Task", Destructive = false, OpenWorld = false)]
    [Description(
        "Creates a single task or updates an existing one. " +
        "Returns OperationResult with the task ID (e.g. 'proj-1-wp-2-task-5') and any cascade state changes. " +
        "To create: provide phaseId with name and description. " +
        "To update: provide taskId plus fields to change (PATCH semantics: null = keep current). " +
        "For creating multiple tasks: use create_or_update_phase with the tasks parameter (batch). " +
        "For state-only changes on multiple tasks: use batch_update_task_states (single call, consolidated cascades). " +
        "For full WP scaffolding: use scaffold_work_package.")]
    public async Task<string> CreateOrUpdateTask(
        [Description("Phase ID (e.g. 'proj-1-wp-2-phase-1'). Required for task creation, ignored for update.")] string? phaseId = null,
        [Description("Task ID (e.g. 'proj-1-wp-2-task-5'). Provide to update an existing task, omit to create.")] string? taskId = null,
        [Description("Task name.")] string? name = null,
        [Description("Task description.")] string? description = null,
        [Description("Sort order for display ordering.")] int? sortOrder = null,
        [Description("Implementation notes (supports markdown).")] string? implementationNotes = null,
        [Description("Completion state (e.g. NotStarted, Implementing, Completed). Omit to keep current.")] CompletionState? state = null,
        [Description("Target files for this task.")] List<FileReferenceInput>? targetFiles = null,
        [Description("File attachments.")] List<FileReferenceInput>? attachments = null,
        CancellationToken ct = default)
    {
        return await ToolErrorHandler.ExecuteAsync(async () =>
        {
            if (taskId is not null)
                return await UpdateExistingTask(taskId, name, description, sortOrder,
                    implementationNotes, state, targetFiles, attachments, ct);

            if (phaseId is null)
                return OperationResult.Error("Either 'phaseId' (for create) or 'taskId' (for update) must be provided.");

            return await CreateNewTask(phaseId, name, description, sortOrder,
                implementationNotes, state, targetFiles, attachments, ct);
        }, "create/update task");
    }

    [McpServerTool(Name = "batch_update_task_states",
        Title = "Batch Update Task States", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "PREFERRED for completing multiple tasks — updates the state of multiple tasks in one operation. " +
        "All tasks must belong to the same work package. " +
        "Cascades (auto-unblock, phase auto-complete, WP auto-complete) run once after ALL transitions, " +
        "returning OperationResult with a consolidated list of state changes. " +
        "Much more efficient than calling create_or_update_task in a loop. " +
        "Does NOT update task fields beyond state (name, description, notes) — " +
        "use create_or_update_task for that.")]
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

}
