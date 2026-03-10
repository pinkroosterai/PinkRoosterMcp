namespace PinkRooster.Mcp.Responses;

public sealed class IssueOverviewItem
{
    public required string IssueId { get; init; }
    public required string Name { get; init; }
    public required string State { get; init; }
    public required string Priority { get; init; }
    public required string Severity { get; init; }
    public required string IssueType { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
}
