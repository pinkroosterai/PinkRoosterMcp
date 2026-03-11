using System.Text.Json.Serialization;

namespace PinkRooster.Shared.DTOs.Requests;

public sealed class UserStoryDto
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("goal")]
    public required string Goal { get; init; }

    [JsonPropertyName("benefit")]
    public required string Benefit { get; init; }
}
