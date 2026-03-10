namespace PinkRooster.Data.Entities;

public sealed class ActivityLog
{
    public long Id { get; set; }
    public required string HttpMethod { get; set; }
    public required string Path { get; set; }
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? CallerIdentity { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
