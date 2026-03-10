namespace PinkRooster.Shared.DTOs.Responses;

public sealed class IssueSummaryResponse
{
    public required int ActiveCount { get; init; }
    public required int InactiveCount { get; init; }
    public required int TerminalCount { get; init; }
    public required List<IssueResponse> LatestTerminalIssues { get; init; }
}
