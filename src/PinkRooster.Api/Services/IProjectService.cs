using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IProjectService
{
    Task<List<ProjectResponse>> GetAllAsync(CancellationToken ct = default);
    Task<ProjectResponse?> GetByPathAsync(string projectPath, CancellationToken ct = default);
    Task<(ProjectResponse Project, bool IsNew)> CreateOrUpdateAsync(
        CreateOrUpdateProjectRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
