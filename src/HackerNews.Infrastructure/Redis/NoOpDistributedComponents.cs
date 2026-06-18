using HackerNews.Application.Abstractions;
using HackerNews.Application.Snapshots;
using HackerNews.Domain;

namespace HackerNews.Infrastructure.Redis;

internal sealed class NoOpDistributedLock : IDistributedLock
{
    private static readonly IAsyncDisposable Handle = new NoOpHandle();

    public Task<IAsyncDisposable> TryAcquireAsync(TimeSpan maxWait, CancellationToken cancellationToken) =>
        Task.FromResult(Handle);

    private sealed class NoOpHandle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class NoOpSnapshotStore : ISnapshotStore
{
    private long _version;

    public Task<SnapshotMeta> GetMetaAsync(CancellationToken cancellationToken) =>
        Task.FromResult<SnapshotMeta>(null);

    public Task<SnapshotState> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<SnapshotState>(null);

    public Task<long> SaveAsync(
        IReadOnlyList<Story> stories, DateTimeOffset updatedAt, CancellationToken cancellationToken) =>
        Task.FromResult(Interlocked.Increment(ref _version));
}

internal sealed class NoOpSnapshotChannel : ISnapshotChannel
{
    public Task PublishAsync(SnapshotState state, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SubscribeAsync(
        Func<SnapshotState, CancellationToken, Task> onSnapshot,
        Func<CancellationToken, Task> onReconnected,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
