using System.Threading.Channels;

namespace PinkRooster.Api.Services;

public sealed class ActivityLogEntry
{
    public required string HttpMethod { get; init; }
    public required string Path { get; init; }
    public int StatusCode { get; init; }
    public long DurationMs { get; init; }
    public string? CallerIdentity { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

public sealed class ActivityLogChannel
{
    private readonly Channel<ActivityLogEntry> _channel = Channel.CreateBounded<ActivityLogEntry>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelReader<ActivityLogEntry> Reader => _channel.Reader;

    public bool TryWrite(ActivityLogEntry entry) => _channel.Writer.TryWrite(entry);
}
