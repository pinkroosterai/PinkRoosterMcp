using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IFeatureRequestService
{
    Task<List<FeatureRequestResponse>> GetByProjectAsync(long projectId, string? stateFilter, CancellationToken ct = default);
    Task<FeatureRequestResponse?> GetByNumberAsync(long projectId, int frNumber, CancellationToken ct = default);
    Task<FeatureRequestResponse> CreateAsync(long projectId, CreateFeatureRequestRequest request, string changedBy, CancellationToken ct = default);
    Task<FeatureRequestResponse?> UpdateAsync(long projectId, int frNumber, UpdateFeatureRequestRequest request, string changedBy, CancellationToken ct = default);
    Task<bool> DeleteAsync(long projectId, int frNumber, CancellationToken ct = default);
}
