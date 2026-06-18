using System.Collections.Immutable;
using HackerNews.Application.Abstractions;
using HackerNews.Domain;

namespace HackerNews.Application.Cache;

public sealed class BestStoriesCache : IBestStoriesCacheReader, IBestStoriesCacheWriter
{
    private sealed record Snapshot(IReadOnlyList<Story> Stories, bool IsReady, long Version);

    private volatile Snapshot _snapshot = new([], false, 0);

    public IReadOnlyList<Story> Current => _snapshot.Stories;
    public bool IsReady => _snapshot.IsReady;
    public long Version => _snapshot.Version;

    public bool TryUpdate(long version, IReadOnlyList<Story> stories)
    {
        if (version <= _snapshot.Version)
            return false;

        _snapshot = new Snapshot(stories.ToImmutableArray(), true, version);
        return true;
    }
}
