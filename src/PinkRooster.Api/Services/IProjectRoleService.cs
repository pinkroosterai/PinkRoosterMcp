using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public interface IProjectRoleService
{
    Task<string?> GetEffectiveRoleAsync(long userId, long projectId, CancellationToken ct = default);
    Task<UserPermissionsResponse> GetUserPermissionsAsync(long userId, long projectId, CancellationToken ct = default);
    Task<UserProjectRoleResponse> AssignRoleAsync(long userId, long projectId, ProjectRole role, CancellationToken ct = default);
    Task<bool> RemoveRoleAsync(long userId, long projectId, CancellationToken ct = default);
    Task<List<UserProjectRoleResponse>> GetProjectRolesAsync(long projectId, CancellationToken ct = default);
}
