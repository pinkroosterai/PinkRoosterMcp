namespace PinkRooster.Shared.DTOs.Responses;

public sealed class WorkPackageSummaryResponse
{
    public int ActiveCount { get; init; }
    public int InactiveCount { get; init; }
    public int TerminalCount { get; init; }
}
