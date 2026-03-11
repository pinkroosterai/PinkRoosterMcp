using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IPhaseService
{
    Task<PhaseResponse> CreateAsync(long projectId, int wpNumber, CreatePhaseRequest request, string changedBy, CancellationToken ct = default);
    Task<PhaseResponse?> UpdateAsync(long projectId, int wpNumber, int phaseNumber, UpdatePhaseRequest request, string changedBy, List<StateChangeDto>? stateChanges = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(long projectId, int wpNumber, int phaseNumber, CancellationToken ct = default);
    Task<PhaseResponse?> VerifyAcceptanceCriteriaAsync(long projectId, int wpNumber, int phaseNumber, VerifyAcceptanceCriteriaRequest request, string changedBy, CancellationToken ct = default);
}
