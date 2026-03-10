using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;

namespace PinkRooster.Api.Services;

public interface IIssueService
{
    Task<List<IssueResponse>> GetByProjectAsync(long projectId, string? stateFilter, CancellationToken ct = default);
    Task<IssueResponse?> GetByNumberAsync(long projectId, int issueNumber, CancellationToken ct = default);
    Task<IssueSummaryResponse> GetSummaryAsync(long projectId, CancellationToken ct = default);
    Task<List<IssueAuditLogResponse>> GetAuditLogAsync(long projectId, int issueNumber, CancellationToken ct = default);
    Task<IssueResponse> CreateAsync(long projectId, CreateIssueRequest request, string changedBy, CancellationToken ct = default);
    Task<IssueResponse?> UpdateAsync(long projectId, int issueNumber, UpdateIssueRequest request, string changedBy, CancellationToken ct = default);
    Task<bool> DeleteAsync(long projectId, int issueNumber, CancellationToken ct = default);
}
