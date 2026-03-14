using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public sealed class ActivityLogService(AppDbContext db) : IActivityLogService
{
    public async Task<PaginatedResponse<ActivityLogResponse>> GetAllAsync(
        PaginationRequest pagination, CancellationToken ct = default)
    {
        var query = db.ActivityLogs.OrderByDescending(x => x.Timestamp);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(x => new ActivityLogResponse
            {
                Id = x.Id,
                HttpMethod = x.HttpMethod,
                Path = x.Path,
                StatusCode = x.StatusCode,
                DurationMs = x.DurationMs,
                CallerIdentity = x.CallerIdentity,
                Timestamp = x.Timestamp
            })
            .ToListAsync(ct);

        return new PaginatedResponse<ActivityLogResponse>
        {
            Items = items,
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            TotalCount = totalCount
        };
    }

}
