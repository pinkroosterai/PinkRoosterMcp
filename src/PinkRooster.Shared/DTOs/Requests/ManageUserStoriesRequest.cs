using PinkRooster.Shared.Enums;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class ManageUserStoriesRequest
{
    public required UserStoryAction Action { get; init; }
    public int? Index { get; init; }
    public string? Role { get; init; }
    public string? Goal { get; init; }
    public string? Benefit { get; init; }
}
