using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using PinkRooster.Shared.DTOs;

namespace PinkRooster.Api.Services;

public sealed class EventBroadcaster : IEventBroadcaster
{
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<Channel<ServerEvent>, byte>> _subscribers = new();

    public async IAsyncEnumerable<ServerEvent> Subscribe(
        long projectId, [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<ServerEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        var set = _subscribers.GetOrAdd(projectId, _ => new());
        set.TryAdd(channel, 0);

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            {
                yield return evt;
            }
        }
        finally
        {
            // Atomic O(1) removal — no snapshot or compare-and-swap needed
            if (_subscribers.TryGetValue(projectId, out var currentSet))
            {
                currentSet.TryRemove(channel, out _);
            }

            channel.Writer.TryComplete();
        }
    }

    public void Publish(ServerEvent serverEvent)
    {
        if (!_subscribers.TryGetValue(serverEvent.ProjectId, out var set))
            return;

        foreach (var channel in set.Keys)
        {
            // Fire-and-forget: try to write, skip if channel is full or completed
            channel.Writer.TryWrite(serverEvent);
        }
    }
}
