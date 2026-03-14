using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IProjectService
{
    Task<List<ProjectResponse>> GetAllAsync(long? userId = null, CancellationToken ct = default);
    Task<ProjectResponse?> GetByPathAsync(string projectPath, CancellationToken ct = default);
    Task<(ProjectResponse Project, bool IsNew)> CreateOrUpdateAsync(
        CreateOrUpdateProjectRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    Task<ProjectStatusResponse?> GetStatusAsync(long projectId, CancellationToken ct = default);
    Task<List<NextActionItem>?> GetNextActionsAsync(long projectId, int limit = 10, string? entityType = null, CancellationToken ct = default);
}
