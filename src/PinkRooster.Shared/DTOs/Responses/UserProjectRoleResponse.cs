namespace PinkRooster.Shared.DTOs.Responses;

public sealed class UserProjectRoleResponse
{
    public required long UserId { get; init; }
    public required string UserEmail { get; init; }
    public required string UserDisplayName { get; init; }
    public required long ProjectId { get; init; }
    public required string Role { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
