namespace PinkRooster.Shared.DTOs.Responses;

public sealed class AuthUserResponse
{
    public required long Id { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required string GlobalRole { get; init; }
    public required bool IsActive { get; init; }
}
