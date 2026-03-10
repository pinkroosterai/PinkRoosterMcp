namespace PinkRooster.Shared.Enums;

public static class FeatureStatusConstants
{
    public static readonly HashSet<FeatureStatus> ActiveStates =
    [
        FeatureStatus.UnderReview,
        FeatureStatus.Approved,
        FeatureStatus.Scheduled,
        FeatureStatus.InProgress
    ];

    public static readonly HashSet<FeatureStatus> InactiveStates =
    [
        FeatureStatus.Proposed,
        FeatureStatus.Deferred
    ];

    public static readonly HashSet<FeatureStatus> TerminalStates =
    [
        FeatureStatus.Completed,
        FeatureStatus.Rejected
    ];
}
