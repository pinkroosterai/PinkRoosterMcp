using System.ComponentModel;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Inputs;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class DependencyTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "manage_dependency",
        Title = "Manage Dependency", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Adds or removes a dependency between work packages or between tasks. " +
        "Entity type is auto-detected from the ID format: " +
        "'proj-N-wp-N' for work packages, 'proj-N-wp-N-task-N' for tasks. " +
        "Both IDs must be the same type (both WP or both task). " +
        "When adding: if the blocker is non-terminal, the dependent auto-transitions to Blocked. " +
        "When the blocker completes, dependents auto-unblock. " +
        "Returns stateChanges showing any automatic state transitions.")]
    public async Task<string> ManageDependency(
        [Description("Dependent entity ID (e.g. 'proj-1-wp-3' or 'proj-1-wp-2-task-3').")] string entityId,
        [Description("Blocker entity ID (e.g. 'proj-1-wp-1' or 'proj-1-wp-2-task-1').")] string dependsOnEntityId,
        [Description("Whether to add or remove the dependency.")] DependencyAction action,
        [Description("Reason for the dependency.")] string? reason = null,
        CancellationToken ct = default)
    {
        // Auto-detect entity type from ID format
        var isTask = IdParser.TryParseTaskId(entityId, out _, out _, out _);
        var isWp = !isTask && IdParser.TryParseWorkPackageId(entityId, out _, out _);

        if (!isTask && !isWp)
            return OperationResult.Error(
                $"Invalid entity ID format: '{entityId}'. " +
                "Expected 'proj-{{number}}-wp-{{number}}' for work packages or " +
                "'proj-{{number}}-wp-{{number}}-task-{{number}}' for tasks.");

        try
        {
            return isTask
                ? await ManageTaskDependency(entityId, dependsOnEntityId, action, reason, ct)
                : await ManageWpDependency(entityId, dependsOnEntityId, action, reason, ct);
        }
        catch (HttpRequestException ex)
        {
            return OperationResult.Error($"Failed to manage dependency: {ex.Message}");
        }
    }

    private async Task<string> ManageWpDependency(
        string entityId, string dependsOnEntityId, DependencyAction action, string? reason, CancellationToken ct)
    {
        if (!IdParser.TryParseWorkPackageId(entityId, out var projId, out var wpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{entityId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        if (!IdParser.TryParseWorkPackageId(dependsOnEntityId, out var depProjId, out var depWpNumber))
            return OperationResult.Error(
                $"Invalid blocker ID format: '{dependsOnEntityId}'. " +
                "Both IDs must be work package IDs ('proj-{{number}}-wp-{{number}}').");

        var dependsOnWp = await apiClient.GetWorkPackageAsync(depProjId, depWpNumber, ct);
        if (dependsOnWp is null)
            return OperationResult.Warning($"Depends-on work package '{dependsOnEntityId}' not found.");

        switch (action)
        {
            case DependencyAction.Add:
                var request = new ManageDependencyRequest
                {
                    DependsOnId = dependsOnWp.Id,
                    Reason = reason
                };
                var depResponse = await apiClient.AddWorkPackageDependencyAsync(projId, wpNumber, request, ct);
                return OperationResult.Success(entityId,
                    $"Dependency added: '{entityId}' is now blocked by '{dependsOnEntityId}'.",
                    stateChanges: depResponse.StateChanges);

            case DependencyAction.Remove:
                var removed = await apiClient.RemoveWorkPackageDependencyAsync(projId, wpNumber, dependsOnWp.Id, ct);
                return removed
                    ? OperationResult.Success(entityId, $"Dependency removed: '{entityId}' is no longer blocked by '{dependsOnEntityId}'.")
                    : OperationResult.Warning($"Dependency between '{entityId}' and '{dependsOnEntityId}' not found.");

            default:
                return OperationResult.Error($"Invalid action: '{action}'.");
        }
    }

    private async Task<string> ManageTaskDependency(
        string entityId, string dependsOnEntityId, DependencyAction action, string? reason, CancellationToken ct)
    {
        if (!IdParser.TryParseTaskId(entityId, out var projId, out var wpNumber, out var taskNumber))
            return OperationResult.Error($"Invalid task ID format: '{entityId}'. Expected 'proj-{{number}}-wp-{{number}}-task-{{number}}'.");

        if (!IdParser.TryParseTaskId(dependsOnEntityId, out var depProjId, out var depWpNumber, out var depTaskNumber))
            return OperationResult.Error(
                $"Invalid blocker ID format: '{dependsOnEntityId}'. " +
                "Both IDs must be task IDs ('proj-{{number}}-wp-{{number}}-task-{{number}}').");

        var dependsOnWp = await apiClient.GetWorkPackageAsync(depProjId, depWpNumber, ct);
        if (dependsOnWp is null)
            return OperationResult.Warning($"Work package containing depends-on task '{dependsOnEntityId}' not found.");

        var dependsOnTask = dependsOnWp.Phases
            .SelectMany(p => p.Tasks)
            .FirstOrDefault(t => t.TaskNumber == depTaskNumber);
        if (dependsOnTask is null)
            return OperationResult.Warning($"Depends-on task '{dependsOnEntityId}' not found.");

        switch (action)
        {
            case DependencyAction.Add:
                var request = new ManageDependencyRequest
                {
                    DependsOnId = dependsOnTask.Id,
                    Reason = reason
                };
                var taskDepResponse = await apiClient.AddTaskDependencyAsync(projId, wpNumber, taskNumber, request, ct);
                return OperationResult.Success(entityId,
                    $"Dependency added: '{entityId}' is now blocked by '{dependsOnEntityId}'.",
                    stateChanges: taskDepResponse.StateChanges);

            case DependencyAction.Remove:
                var removed = await apiClient.RemoveTaskDependencyAsync(projId, wpNumber, taskNumber, dependsOnTask.Id, ct);
                return removed
                    ? OperationResult.Success(entityId, $"Dependency removed: '{entityId}' is no longer blocked by '{dependsOnEntityId}'.")
                    : OperationResult.Warning($"Dependency between '{entityId}' and '{dependsOnEntityId}' not found.");

            default:
                return OperationResult.Error($"Invalid action: '{action}'.");
        }
    }
}
