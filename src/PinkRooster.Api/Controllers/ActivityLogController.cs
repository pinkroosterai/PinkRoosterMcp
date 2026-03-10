using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Services;
using PinkRooster.Shared.Constants;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route(ApiRoutes.ActivityLogs.Route)]
public sealed class ActivityLogController(IActivityLogService activityLogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ActivityLogResponse>>> GetAll(
        [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        var result = await activityLogService.GetAllAsync(pagination, ct);
        return Ok(result);
    }
}
