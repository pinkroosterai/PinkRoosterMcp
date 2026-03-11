using PinkRooster.Shared.DTOs;

namespace PinkRooster.Api.Services;

public interface IEventBroadcaster
{
    IAsyncEnumerable<ServerEvent> Subscribe(long projectId, CancellationToken ct);
    void Publish(ServerEvent serverEvent);
    bool TryAcquireConnection(long projectId);
    void ReleaseConnection(long projectId);
}
