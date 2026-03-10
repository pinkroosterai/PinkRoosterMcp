namespace PinkRooster.Shared.DTOs.Requests;

public sealed class CreateOrUpdateProjectRequest
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ProjectPath { get; init; }
}
