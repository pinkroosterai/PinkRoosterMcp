using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using PinkRooster.Shared.DTOs;

namespace PinkRooster.Api.Services;

public sealed class EventBroadcaster : IEventBroadcaster
{
    private readonly ConcurrentDictionary<long, ConcurrentBag<Channel<ServerEvent>>> _subscribers = new();

    public async IAsyncEnumerable<ServerEvent> Subscribe(
        long projectId, [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<ServerEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        var bag = _subscribers.GetOrAdd(projectId, _ => []);
        bag.Add(channel);

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            {
                yield return evt;
            }
        }
        finally
        {
            // Remove this channel from the subscriber list
            if (_subscribers.TryGetValue(projectId, out var currentBag))
            {
                var remaining = new ConcurrentBag<Channel<ServerEvent>>(
                    currentBag.Where(c => c != channel));
                _subscribers.TryUpdate(projectId, remaining, currentBag);
            }

            channel.Writer.TryComplete();
        }
    }

    public void Publish(ServerEvent serverEvent)
    {
        if (!_subscribers.TryGetValue(serverEvent.ProjectId, out var bag))
            return;

        foreach (var channel in bag)
        {
            // Fire-and-forget: try to write, skip if channel is full or completed
            channel.Writer.TryWrite(serverEvent);
        }
    }
}
