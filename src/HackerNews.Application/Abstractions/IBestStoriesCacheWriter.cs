using HackerNews.Domain;

namespace HackerNews.Application.Abstractions;

public interface IBestStoriesCacheWriter
{
    bool TryUpdate(long version, IReadOnlyList<Story> stories);
}
