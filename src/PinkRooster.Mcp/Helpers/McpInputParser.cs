using PinkRooster.Mcp.Inputs;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Mcp.Helpers;

internal static class McpInputParser
{
    private static readonly HashSet<string> TerminalStateStrings =
        CompletionStateConstants.TerminalStates.Select(s => s.ToString()).ToHashSet();

    internal static bool IsTerminalState(string state) =>
        TerminalStateStrings.Contains(state);

    internal static List<T>? NullIfEmpty<T>(List<T> list) =>
        list.Count == 0 ? null : list;

    // ── Mapping: MCP inputs → Shared DTOs ──

    internal static List<FileReferenceDto>? MapFileReferences(List<FileReferenceInput>? inputs) =>
        inputs?.Select(f => new FileReferenceDto
        {
            FileName = f.FileName,
            RelativePath = f.RelativePath,
            Description = f.Description
        }).ToList();

    internal static List<AcceptanceCriterionDto>? MapAcceptanceCriteria(List<AcceptanceCriterionInput>? inputs) =>
        inputs?.Select(ac => new AcceptanceCriterionDto
        {
            Name = ac.Name,
            Description = ac.Description,
            VerificationMethod = ac.VerificationMethod ?? VerificationMethod.Manual
        }).ToList();

    internal static List<CreateTaskRequest>? MapCreateTasks(List<PhaseTaskInput>? inputs) =>
        inputs?.Select(t => new CreateTaskRequest
        {
            Name = t.Name ?? "",
            Description = t.Description ?? "",
            SortOrder = t.SortOrder,
            ImplementationNotes = t.ImplementationNotes,
            State = t.State ?? CompletionState.NotStarted,
            TargetFiles = MapFileReferences(t.TargetFiles),
            Attachments = MapFileReferences(t.Attachments)
        }).ToList();

    internal static List<UpsertTaskInPhaseDto>? MapUpsertTasks(List<PhaseTaskInput>? inputs) =>
        inputs?.Select(t => new UpsertTaskInPhaseDto
        {
            TaskNumber = t.TaskNumber,
            Name = t.Name,
            Description = t.Description,
            SortOrder = t.SortOrder,
            ImplementationNotes = t.ImplementationNotes,
            State = t.State,
            TargetFiles = MapFileReferences(t.TargetFiles),
            Attachments = MapFileReferences(t.Attachments)
        }).ToList();

    internal static List<ScaffoldPhaseRequest> MapScaffoldPhases(List<ScaffoldPhaseInput> inputs) =>
        inputs.Select(p => new ScaffoldPhaseRequest
        {
            Name = p.Name,
            Description = p.Description,
            SortOrder = p.SortOrder,
            AcceptanceCriteria = MapAcceptanceCriteria(p.AcceptanceCriteria),
            Tasks = p.Tasks?.Select(t => new ScaffoldTaskRequest
            {
                Name = t.Name,
                Description = t.Description,
                SortOrder = t.SortOrder,
                ImplementationNotes = t.ImplementationNotes,
                State = t.State ?? CompletionState.NotStarted,
                TargetFiles = MapFileReferences(t.TargetFiles),
                Attachments = MapFileReferences(t.Attachments),
                DependsOnTaskIndices = t.DependsOnTaskIndices
            }).ToList()
        }).ToList();
}
