using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IWorkPackageScaffoldingService
{
    Task<ScaffoldWorkPackageResponse> ScaffoldAsync(
        long projectId, ScaffoldWorkPackageRequest request, string changedBy,
        List<StateChangeDto>? stateChanges = null, CancellationToken ct = default);
}
