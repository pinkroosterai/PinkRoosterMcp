using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public interface IUserService
{
    Task<List<AuthUserResponse>> GetAllAsync(CancellationToken ct = default);
    Task<AuthUserResponse?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<AuthUserResponse> CreateAsync(string email, string password, string displayName, GlobalRole globalRole, CancellationToken ct = default);
    Task<AuthUserResponse?> UpdateAsync(long id, string? displayName, string? email, GlobalRole? globalRole, bool? isActive, CancellationToken ct = default);
    Task<bool> DeactivateAsync(long id, CancellationToken ct = default);
    Task<AuthUserResponse?> UpdateProfileAsync(long userId, string? displayName, string? email, CancellationToken ct = default);
    Task<bool> ChangePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<bool> VerifyPasswordAsync(long userId, string password, CancellationToken ct = default);
}
