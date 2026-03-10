using System.ComponentModel;
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
public sealed class TaskTools(PinkRoosterApiClient apiClient)
{
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
            SortOrder = McpInputParser.ParseInt(sortOrder),
            ImplementationNotes = implementationNotes,
            State = McpInputParser.ParseEnumOrDefault(state, CompletionState.NotStarted),
            TargetFiles = McpInputParser.ParseFileReferences(targetFiles),
            Attachments = McpInputParser.ParseFileReferences(attachments)
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
            SortOrder = McpInputParser.ParseInt(sortOrder),
            ImplementationNotes = implementationNotes,
            State = state is not null ? McpInputParser.ParseEnum<CompletionState>(state) : null,
            TargetFiles = targetFiles is not null ? McpInputParser.ParseFileReferences(targetFiles) : null,
            Attachments = attachments is not null ? McpInputParser.ParseFileReferences(attachments) : null
        };

        var updated = await apiClient.UpdateTaskAsync(projId, wpNumber, taskNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Task '{taskId}' not found.");

        return OperationResult.Success(taskId, $"Task '{taskId}' updated.",
            stateChanges: updated.StateChanges);
    }
}
