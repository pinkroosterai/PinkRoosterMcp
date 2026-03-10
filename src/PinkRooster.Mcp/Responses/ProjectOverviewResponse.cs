namespace PinkRooster.Mcp.Responses;

public sealed class ProjectOverviewResponse
{
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ProjectPath { get; init; }
    public required string Status { get; init; }
    public List<IssueOverviewItem> ActiveIssues { get; set; } = [];
    public List<IssueOverviewItem> InactiveIssues { get; set; } = [];
    public List<IssueOverviewItem> LatestTerminalIssues { get; set; } = [];
    public List<WorkPackageOverviewItem> ActiveWorkPackages { get; set; } = [];
    public List<WorkPackageOverviewItem> InactiveWorkPackages { get; set; } = [];
    public int TerminalWorkPackageCount { get; set; }
}
