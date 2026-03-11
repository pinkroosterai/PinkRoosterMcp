using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using PinkRooster.Shared.DTOs;

namespace PinkRooster.Api.Services;

public sealed class EventBroadcaster : IEventBroadcaster
{
    private const int MaxConnectionsPerProject = 10;

    private readonly ConcurrentDictionary<long, ConcurrentDictionary<Channel<ServerEvent>, byte>> _subscribers = new();
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _connectionLimits = new();

    public bool TryAcquireConnection(long projectId)
    {
        var semaphore = _connectionLimits.GetOrAdd(projectId, _ => new SemaphoreSlim(MaxConnectionsPerProject, MaxConnectionsPerProject));
        return semaphore.Wait(0);
    }

    public void ReleaseConnection(long projectId)
    {
        if (_connectionLimits.TryGetValue(projectId, out var semaphore))
            semaphore.Release();
    }

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

                // Clean up empty outer key to prevent unbounded accumulation
                if (currentSet.IsEmpty)
                {
                    _subscribers.TryRemove(new KeyValuePair<long,
                        ConcurrentDictionary<Channel<ServerEvent>, byte>>(projectId, currentSet));
                }
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
