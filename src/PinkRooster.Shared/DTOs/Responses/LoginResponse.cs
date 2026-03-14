namespace PinkRooster.Shared.DTOs.Responses;

public sealed class LoginResponse
{
    public required AuthUserResponse User { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
