namespace PinkRooster.Shared.DTOs.Responses;

public sealed class LinkedWorkPackageItem
{
    public required string WorkPackageId { get; init; }
    public required string Name { get; init; }
    public required string State { get; init; }
    public required string Type { get; init; }
    public required string Priority { get; init; }
}
