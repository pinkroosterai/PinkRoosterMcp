namespace PinkRooster.Shared.DTOs.Requests;

public sealed class ManageUserStoriesRequest
{
    public required string Action { get; init; } // Add, Update, Remove
    public int? Index { get; init; }
    public string? Role { get; init; }
    public string? Goal { get; init; }
    public string? Benefit { get; init; }
}
