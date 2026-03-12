namespace PinkRooster.Shared.DTOs.Responses;

public sealed class ProjectMemoryListItemResponse
{
    public required string MemoryId { get; init; }
    public required string Name { get; init; }
    public required List<string> Tags { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
