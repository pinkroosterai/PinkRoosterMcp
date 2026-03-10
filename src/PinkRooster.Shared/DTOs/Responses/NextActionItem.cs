namespace PinkRooster.Shared.DTOs.Responses;

public sealed class NextActionItem
{
    public required string Type { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Priority { get; init; }
    public required string State { get; init; }
    public required string ParentId { get; init; }
}
