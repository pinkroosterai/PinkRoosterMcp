using System.Text.Json.Serialization;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Mcp.Responses;

public sealed class ScaffoldOperationResult
{
    public required ResponseType ResponseType { get; init; }
    public required string Message { get; init; }
    public required string Id { get; init; }
    public required List<ScaffoldPhaseResult> Phases { get; init; }
    public int TotalTasks { get; init; }
    public int TotalDependencies { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StateChangeDto>? StateChanges { get; init; }
}
