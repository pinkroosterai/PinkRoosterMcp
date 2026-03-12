using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IProjectMemoryService
{
    Task<List<ProjectMemoryListItemResponse>> GetByProjectAsync(
        long projectId, string? namePattern, string? tag, CancellationToken ct = default);

    Task<ProjectMemoryResponse?> GetByNumberAsync(
        long projectId, int memoryNumber, CancellationToken ct = default);

    Task<ProjectMemoryResponse> UpsertAsync(
        long projectId, UpsertProjectMemoryRequest request, string changedBy, CancellationToken ct = default);

    Task<bool> DeleteAsync(long projectId, int memoryNumber, CancellationToken ct = default);
}
