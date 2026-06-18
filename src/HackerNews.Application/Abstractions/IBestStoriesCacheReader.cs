using HackerNews.Domain;

namespace HackerNews.Application.Abstractions;

public interface IBestStoriesCacheReader
{
    IReadOnlyList<Story> Current { get; }
    bool IsReady { get; }
    long Version { get; }
}
