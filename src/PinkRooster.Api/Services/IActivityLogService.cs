using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IActivityLogService
{
    Task<PaginatedResponse<ActivityLogResponse>> GetAllAsync(PaginationRequest pagination, CancellationToken ct = default);

    Task LogRequestAsync(string httpMethod, string path, int statusCode, long durationMs,
        string? callerIdentity, CancellationToken ct = default);
}
