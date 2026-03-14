using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IAuthService
{
    Task<AuthUserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<(LoginResponse Response, string SessionToken)?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task LogoutAsync(string sessionToken, CancellationToken ct = default);
    Task<AuthUserResponse?> GetCurrentUserAsync(string sessionToken, CancellationToken ct = default);
    Task<User?> ValidateSessionAsync(string sessionToken, CancellationToken ct = default);
    Task<bool> HasAnyUsersAsync(CancellationToken ct = default);
}
