using HackerNews.Domain;

namespace HackerNews.Application.Abstractions;

public interface IHackerNewsClient
{
    Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken);
    Task<Story> GetStoryAsync(long id, CancellationToken cancellationToken);
}
