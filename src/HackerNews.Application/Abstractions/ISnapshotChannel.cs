using HackerNews.Application.Snapshots;

namespace HackerNews.Application.Abstractions;

public interface ISnapshotChannel
{
    Task PublishAsync(SnapshotState state, CancellationToken cancellationToken);

    Task SubscribeAsync(
        Func<SnapshotState, CancellationToken, Task> onSnapshot,
        Func<CancellationToken, Task> onReconnected,
        CancellationToken cancellationToken);
}
