using Microsoft.AspNetCore.Mvc;
using PinkRooster.Api.Services;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:long}/issues")]
public sealed class IssueController(IIssueService issueService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<IssueResponse>>> GetAll(
        long projectId, [FromQuery] string? state, CancellationToken ct)
    {
        var issues = await issueService.GetByProjectAsync(projectId, state, ct);
        return Ok(issues);
    }

    [HttpGet("{issueNumber:int}")]
    public async Task<ActionResult<IssueResponse>> GetByNumber(
        long projectId, int issueNumber, CancellationToken ct)
    {
        var issue = await issueService.GetByNumberAsync(projectId, issueNumber, ct);
        return issue is null ? NotFound() : Ok(issue);
    }

    [HttpGet("{issueNumber:int}/audit")]
    public async Task<ActionResult<List<IssueAuditLogResponse>>> GetAuditLog(
        long projectId, int issueNumber, CancellationToken ct)
    {
        var logs = await issueService.GetAuditLogAsync(projectId, issueNumber, ct);
        return Ok(logs);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<IssueSummaryResponse>> GetSummary(
        long projectId, CancellationToken ct)
    {
        var summary = await issueService.GetSummaryAsync(projectId, ct);
        return Ok(summary);
    }

    [HttpPost]
    public async Task<ActionResult<IssueResponse>> Create(
        long projectId, CreateIssueRequest request, CancellationToken ct)
    {
        var changedBy = GetCallerIdentity();
        var issue = await issueService.CreateAsync(projectId, request, changedBy, ct);
        return Created($"api/projects/{projectId}/issues/{issue.IssueNumber}", issue);
    }

    [HttpPatch("{issueNumber:int}")]
    public async Task<ActionResult<IssueResponse>> Update(
        long projectId, int issueNumber, UpdateIssueRequest request, CancellationToken ct)
    {
        var changedBy = GetCallerIdentity();
        var issue = await issueService.UpdateAsync(projectId, issueNumber, request, changedBy, ct);
        return issue is null ? NotFound() : Ok(issue);
    }

    [HttpDelete("{issueNumber:int}")]
    public async Task<ActionResult> Delete(
        long projectId, int issueNumber, CancellationToken ct)
    {
        var deleted = await issueService.DeleteAsync(projectId, issueNumber, ct);
        return deleted ? NoContent() : NotFound();
    }

    private string GetCallerIdentity()
    {
        return HttpContext.Items["CallerIdentity"] as string ?? "unknown";
    }
}
