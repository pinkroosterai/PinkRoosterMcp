namespace PinkRooster.Shared.DTOs.Requests;

public sealed class UpsertProjectMemoryRequest
{
    public required string Name { get; init; }
    public required string Content { get; init; }
    public List<string>? Tags { get; init; }
}
