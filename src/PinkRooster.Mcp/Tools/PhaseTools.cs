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
public sealed class PhaseTools(PinkRoosterApiClient apiClient)
{
    [McpServerTool(Name = "create_or_update_phase",
        Title = "Create or Update Phase", Destructive = false, OpenWorld = false)]
    [Description(
        "Creates a new phase or updates an existing one. Can include tasks for batch creation/update. " +
        "To create: provide workPackageId and name. To update: provide phaseId plus fields to change. " +
        "For creating a full WP with phases and tasks at once, use scaffold_work_package instead.")]
    public async Task<string> CreateOrUpdatePhase(
        [Description("Work package ID (e.g. 'proj-1-wp-2').")] string workPackageId,
        [Description("Phase ID (e.g. 'proj-1-wp-2-phase-1'). Omit to create a new phase.")] string? phaseId = null,
        [Description("Phase name.")] string? name = null,
        [Description("Phase description.")] string? description = null,
        [Description("Sort order for display ordering.")] int? sortOrder = null,
        [Description("Completion state (e.g. NotStarted, Implementing, Completed). Omit to keep current.")] CompletionState? state = null,
        [Description("Acceptance criteria for this phase. Replaces all existing criteria on update.")] List<AcceptanceCriterionInput>? acceptanceCriteria = null,
        [Description("Tasks to create or update. For new tasks: provide name and description. For existing tasks: provide taskNumber and fields to change.")] List<PhaseTaskInput>? tasks = null,
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

    private async Task<string> CreateNewPhase(
        long projId, int wpNumber, string? name, string? description, int? sortOrder,
        List<AcceptanceCriterionInput>? acceptanceCriteria, List<PhaseTaskInput>? tasks, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Error("'name' is required when creating a phase.");

        var request = new CreatePhaseRequest
        {
            Name = name,
            Description = description,
            SortOrder = sortOrder,
            AcceptanceCriteria = McpInputParser.MapAcceptanceCriteria(acceptanceCriteria),
            Tasks = McpInputParser.MapCreateTasks(tasks)
        };

        var created = await apiClient.CreatePhaseAsync(projId, wpNumber, request, ct);
        return OperationResult.Success(created.PhaseId, $"Phase '{name}' created.");
    }

    private async Task<string> UpdateExistingPhase(
        long projId, int wpNumber, string phaseId, string? name, string? description,
        int? sortOrder, CompletionState? state, List<AcceptanceCriterionInput>? acceptanceCriteria,
        List<PhaseTaskInput>? tasks, CancellationToken ct)
    {
        if (!IdParser.TryParsePhaseId(phaseId, out var parsedProjId, out var parsedWpNumber, out var phaseNumber))
            return OperationResult.Error($"Invalid phase ID format: '{phaseId}'. Expected 'proj-{{number}}-wp-{{number}}-phase-{{number}}'.");

        if (parsedProjId != projId || parsedWpNumber != wpNumber)
            return OperationResult.Error($"Phase ID '{phaseId}' does not belong to work package 'proj-{projId}-wp-{wpNumber}'.");

        var request = new UpdatePhaseRequest
        {
            Name = name,
            Description = description,
            SortOrder = sortOrder,
            State = state,
            AcceptanceCriteria = McpInputParser.MapAcceptanceCriteria(acceptanceCriteria),
            Tasks = McpInputParser.MapUpsertTasks(tasks)
        };

        var updated = await apiClient.UpdatePhaseAsync(projId, wpNumber, phaseNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Phase '{phaseId}' not found.");

        return OperationResult.Success(phaseId, $"Phase '{phaseId}' updated.",
            stateChanges: updated.StateChanges);
    }
}
