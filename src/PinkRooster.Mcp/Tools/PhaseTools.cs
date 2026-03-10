using System.ComponentModel;
using ModelContextProtocol.Server;
using PinkRooster.Mcp.Clients;
using PinkRooster.Mcp.Helpers;
using PinkRooster.Mcp.Responses;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.Enums;
using PinkRooster.Shared.Helpers;

namespace PinkRooster.Mcp.Tools;

[McpServerToolType]
public sealed class PhaseTools(PinkRoosterApiClient apiClient)
{
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
            SortOrder = McpInputParser.ParseInt(sortOrder),
            AcceptanceCriteria = McpInputParser.ParseAcceptanceCriteria(acceptanceCriteria),
            Tasks = McpInputParser.ParseCreateTasks(tasks)
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
            SortOrder = McpInputParser.ParseInt(sortOrder),
            State = state is not null ? McpInputParser.ParseEnum<CompletionState>(state) : null,
            AcceptanceCriteria = McpInputParser.ParseAcceptanceCriteria(acceptanceCriteria),
            Tasks = McpInputParser.ParseUpsertTasks(tasks)
        };

        var updated = await apiClient.UpdatePhaseAsync(projId, wpNumber, phaseNumber, request, ct);
        if (updated is null)
            return OperationResult.Warning($"Phase '{phaseId}' not found.");

        return OperationResult.Success(phaseId, $"Phase '{phaseId}' updated.",
            stateChanges: updated.StateChanges);
    }
}
