using HackerNews.Application.Snapshots;
using HackerNews.Domain;

namespace HackerNews.Application.Abstractions;

public interface ISnapshotStore
{
    Task<SnapshotMeta> GetMetaAsync(CancellationToken cancellationToken);

    Task<SnapshotState> LoadAsync(CancellationToken cancellationToken);

    Task<long> SaveAsync(IReadOnlyList<Story> stories, DateTimeOffset updatedAt, CancellationToken cancellationToken);
}
