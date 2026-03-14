using System.Threading.Channels;

namespace PinkRooster.Api.Services;

public sealed class WebhookEvent
{
    public long ProjectId { get; init; }
    public required string EventType { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required object Payload { get; init; }
}

public sealed class WebhookEventChannel
{
    private readonly Channel<WebhookEvent> _channel = Channel.CreateBounded<WebhookEvent>(
        new BoundedChannelOptions(1_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelReader<WebhookEvent> Reader => _channel.Reader;

    public bool TryWrite(WebhookEvent entry) => _channel.Writer.TryWrite(entry);
}
