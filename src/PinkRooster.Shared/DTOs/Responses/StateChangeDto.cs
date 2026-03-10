namespace PinkRooster.Shared.DTOs.Responses;

public sealed class StateChangeDto
{
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string OldState { get; init; }
    public required string NewState { get; init; }
    public required string Reason { get; init; }
}
