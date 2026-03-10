namespace PinkRooster.Mcp.Responses;

public sealed class WorkPackageOverviewItem
{
    public required string WorkPackageId { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Priority { get; init; }
    public required string State { get; init; }
    public int PhaseCount { get; init; }
    public int TaskCount { get; init; }
    public int CompletedTaskCount { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
}
