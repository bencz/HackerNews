using HackerNews.Domain;

namespace HackerNews.Application.Abstractions;

public interface IBestStoriesService
{
    Task<IReadOnlyList<Story>> RefreshAsync(CancellationToken cancellationToken);
    IReadOnlyList<Story> GetBestStories(int? totalStoriesToFetch);
}
