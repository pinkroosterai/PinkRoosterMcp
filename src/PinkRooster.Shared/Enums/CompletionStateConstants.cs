namespace PinkRooster.Shared.Enums;

public static class CompletionStateConstants
{
    public static readonly HashSet<CompletionState> ActiveStates =
    [
        CompletionState.Designing,
        CompletionState.Implementing,
        CompletionState.Testing,
        CompletionState.InReview
    ];

    public static readonly HashSet<CompletionState> InactiveStates =
    [
        CompletionState.Blocked,
        CompletionState.NotStarted
    ];

    public static readonly HashSet<CompletionState> TerminalStates =
    [
        CompletionState.Completed,
        CompletionState.Cancelled,
        CompletionState.Replaced
    ];
}
