using System.Text.Json.Serialization;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Shared.DTOs;

public sealed class ServerEvent
{
    public required string EventType { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string Action { get; init; }
    public required long ProjectId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StateChangeDto>? StateChanges { get; init; }
}
