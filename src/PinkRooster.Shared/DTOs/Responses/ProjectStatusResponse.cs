namespace PinkRooster.Shared.DTOs.Responses;

public sealed class ProjectStatusResponse
{
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public required EntityStatusSummary Issues { get; init; }
    public required EntityStatusSummary FeatureRequests { get; init; }
    public required WorkPackageStatusSummary WorkPackages { get; init; }
}

public sealed class EntityStatusSummary
{
    public required int Total { get; init; }
    public required int Active { get; init; }
    public required int Inactive { get; init; }
    public required int Terminal { get; init; }
    public required int PercentComplete { get; init; }
    public required List<StatusItem> ActiveItems { get; init; }
    public required List<StatusItem> InactiveItems { get; init; }
}

public sealed class WorkPackageStatusSummary
{
    public required int Total { get; init; }
    public required int TerminalCount { get; init; }
    public required int PercentComplete { get; init; }
    public required List<StatusItem> Active { get; init; }
    public required List<StatusItem> Inactive { get; init; }
    public required List<StatusItem> Blocked { get; init; }
}

public sealed class StatusItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}
