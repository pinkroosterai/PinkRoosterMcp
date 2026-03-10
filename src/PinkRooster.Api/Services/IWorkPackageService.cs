using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IWorkPackageService
{
    Task<List<WorkPackageResponse>> GetByProjectAsync(long projectId, string? stateFilter, CancellationToken ct = default);
    Task<WorkPackageResponse?> GetByNumberAsync(long projectId, int wpNumber, CancellationToken ct = default);
    Task<WorkPackageSummaryResponse> GetSummaryAsync(long projectId, CancellationToken ct = default);
    Task<WorkPackageResponse> CreateAsync(long projectId, CreateWorkPackageRequest request, string changedBy, CancellationToken ct = default);
    Task<WorkPackageResponse?> UpdateAsync(long projectId, int wpNumber, UpdateWorkPackageRequest request, string changedBy, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(long projectId, int wpNumber, CancellationToken ct = default);
    Task<DependencyResponse> AddDependencyAsync(long projectId, int wpNumber, ManageDependencyRequest request, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default);
    Task<bool> RemoveDependencyAsync(long projectId, int wpNumber, long dependsOnWpId, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default);
    Task<ScaffoldWorkPackageResponse> ScaffoldAsync(long projectId, ScaffoldWorkPackageRequest request, string changedBy, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default);
}
