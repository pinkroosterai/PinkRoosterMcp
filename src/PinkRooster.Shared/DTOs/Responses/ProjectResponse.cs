namespace PinkRooster.Shared.DTOs.Responses;

public sealed class ProjectResponse
{
    public required string ProjectId { get; init; }
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ProjectPath { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
