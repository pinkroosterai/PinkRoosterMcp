using System.ComponentModel;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Inputs;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class DeleteTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "delete_entity",
        Title = "Delete Entity", Destructive = true, OpenWorld = false)]
    [Description(
        "Permanently deletes an entity. Work package deletion cascades to all its phases and tasks. " +
        "Phase deletion cascades to all its tasks. Issue/FR deletion clears WP links (WPs are NOT deleted). " +
        "This action cannot be undone.")]
    public async Task<string> DeleteEntity(
        [Description("Type of entity to delete.")] DeleteEntityType entityType,
        [Description("Entity ID (e.g. 'proj-1-issue-3', 'proj-1-fr-2', 'proj-1-wp-1', 'proj-1-wp-1-phase-1', 'proj-1-wp-1-task-3').")] string entityId,
        CancellationToken ct = default)
    {
        try
        {
            return entityType switch
            {
                DeleteEntityType.Issue => await DeleteIssueAsync(entityId, ct),
                DeleteEntityType.FeatureRequest => await DeleteFeatureRequestAsync(entityId, ct),
                DeleteEntityType.WorkPackage => await DeleteWorkPackageAsync(entityId, ct),
                DeleteEntityType.Phase => await DeletePhaseAsync(entityId, ct),
                DeleteEntityType.Task => await DeleteTaskAsync(entityId, ct),
                _ => OperationResult.Error($"Unknown entity type: '{entityType}'.")
            };
        }
        catch (HttpRequestException ex)
        {
            return OperationResult.Error($"API error: {ex.Message}");
        }
    }

    private async Task<string> DeleteIssueAsync(string entityId, CancellationToken ct)
    {
        if (!IdParser.TryParseIssueId(entityId, out var projId, out var issueNumber))
            return OperationResult.Error($"Invalid issue ID format: '{entityId}'. Expected 'proj-{{number}}-issue-{{number}}'.");

        var deleted = await apiClient.DeleteIssueAsync(projId, issueNumber, ct);
        return deleted
            ? OperationResult.Success(entityId, $"Deleted issue '{entityId}'.")
            : OperationResult.Warning($"Issue '{entityId}' not found.");
    }

    private async Task<string> DeleteFeatureRequestAsync(string entityId, CancellationToken ct)
    {
        if (!IdParser.TryParseFeatureRequestId(entityId, out var projId, out var frNumber))
            return OperationResult.Error($"Invalid feature request ID format: '{entityId}'. Expected 'proj-{{number}}-fr-{{number}}'.");

        var deleted = await apiClient.DeleteFeatureRequestAsync(projId, frNumber, ct);
        return deleted
            ? OperationResult.Success(entityId, $"Deleted feature request '{entityId}'.")
            : OperationResult.Warning($"Feature request '{entityId}' not found.");
    }

    private async Task<string> DeleteWorkPackageAsync(string entityId, CancellationToken ct)
    {
        if (!IdParser.TryParseWorkPackageId(entityId, out var projId, out var wpNumber))
            return OperationResult.Error($"Invalid work package ID format: '{entityId}'. Expected 'proj-{{number}}-wp-{{number}}'.");

        var deleted = await apiClient.DeleteWorkPackageAsync(projId, wpNumber, ct);
        return deleted
            ? OperationResult.Success(entityId, $"Deleted work package '{entityId}' and all its phases and tasks.")
            : OperationResult.Warning($"Work package '{entityId}' not found.");
    }

    private async Task<string> DeletePhaseAsync(string entityId, CancellationToken ct)
    {
        if (!IdParser.TryParsePhaseId(entityId, out var projId, out var wpNumber, out var phaseNumber))
            return OperationResult.Error($"Invalid phase ID format: '{entityId}'. Expected 'proj-{{number}}-wp-{{number}}-phase-{{number}}'.");

        var deleted = await apiClient.DeletePhaseAsync(projId, wpNumber, phaseNumber, ct);
        return deleted
            ? OperationResult.Success(entityId, $"Deleted phase '{entityId}' and all its tasks.")
            : OperationResult.Warning($"Phase '{entityId}' not found.");
    }

    private async Task<string> DeleteTaskAsync(string entityId, CancellationToken ct)
    {
        if (!IdParser.TryParseTaskId(entityId, out var projId, out var wpNumber, out var taskNumber))
            return OperationResult.Error($"Invalid task ID format: '{entityId}'. Expected 'proj-{{number}}-wp-{{number}}-task-{{number}}'.");

        var deleted = await apiClient.DeleteTaskAsync(projId, wpNumber, taskNumber, ct);
        return deleted
            ? OperationResult.Success(entityId, $"Deleted task '{entityId}'.")
            : OperationResult.Warning($"Task '{entityId}' not found.");
    }
}
