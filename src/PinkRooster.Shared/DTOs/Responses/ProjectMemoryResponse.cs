namespace PinkRooster.Shared.DTOs.Responses;

public sealed class ProjectMemoryResponse
{
    public required string MemoryId { get; init; }
    public required string ProjectId { get; init; }
    public required int MemoryNumber { get; init; }
    public required string Name { get; init; }
    public required string Content { get; init; }
    public required List<string> Tags { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public bool WasMerged { get; init; }
}
