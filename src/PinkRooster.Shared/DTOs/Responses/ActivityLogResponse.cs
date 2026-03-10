namespace PinkRooster.Shared.DTOs.Responses;

public sealed class ActivityLogResponse
{
    public required long Id { get; init; }
    public required string HttpMethod { get; init; }
    public required string Path { get; init; }
    public required int StatusCode { get; init; }
    public required long DurationMs { get; init; }
    public string? CallerIdentity { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
