using System.Diagnostics;
using HackerNews.Application.Abstractions;
using HackerNews.Application.Configuration;
using HackerNews.Application.Exceptions;
using HackerNews.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HackerNews.Application.Services;

public sealed class BestStoriesService : IBestStoriesService
{
    private readonly IHackerNewsClient _hackerNewsClient;
    private readonly IBestStoriesCacheReader _bestStoriesCacheReader;
    private readonly HackerNewsOptions _hackerNewsOptions;
    private readonly ILogger<BestStoriesService> _logger;

    public BestStoriesService(
        IHackerNewsClient hackerNewsClient,
        IBestStoriesCacheReader bestStoriesCacheReader,
        IOptions<HackerNewsOptions> hackerNewsOptions,
        ILogger<BestStoriesService> logger)
    {
        _hackerNewsClient = hackerNewsClient;
        _bestStoriesCacheReader = bestStoriesCacheReader;
        _hackerNewsOptions = hackerNewsOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Story>> RefreshAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        var ids = await _hackerNewsClient.GetBestStoryIdsAsync(cancellationToken);
        if (ids.Count == 0)
        {
            _logger.LogWarning("Hacker News returned an empty list; keeping previous snapshot");
            return null;
        }

        var snapshotSize = Math.Min(ids.Count, _hackerNewsOptions.SnapshotSize);
        var idsToFetch = ids.Take(snapshotSize).ToArray();

        var stories = await FetchStoriesAsync(idsToFetch, cancellationToken);

        var failed = idsToFetch.Length - stories.Count;
        if (idsToFetch.Length > 0 &&
            (double)failed / idsToFetch.Length > _hackerNewsOptions.MaxFailureRatio)
        {
            throw new RefreshFailedException(
                $"Refresh aborted: {failed}/{idsToFetch.Length} story fetches failed " +
                $"(above the {_hackerNewsOptions.MaxFailureRatio:P0} threshold); keeping previous snapshot");
        }

        var ordered = stories.OrderByDescending(s => s.Score).ToArray();

        _logger.LogInformation(
            "Fetched {Count} stories in {ElapsedSeconds:F2} s",
            ordered.Length, sw.Elapsed.TotalSeconds);

        return ordered;
    }

    public IReadOnlyList<Story> GetBestStories(int? totalStoriesToFetch)
    {
        if (totalStoriesToFetch is <= 0)
            throw new ValidationException("Number of stories must be positive");

        if (!_bestStoriesCacheReader.IsReady)
            throw new StoriesCacheNotReadyException();

        var current = _bestStoriesCacheReader.Current;

        return (totalStoriesToFetch != null && totalStoriesToFetch < current.Count)
            ? current.Take(totalStoriesToFetch.Value).ToArray()
            : current;
    }

    private async Task<IReadOnlyList<Story>> FetchStoriesAsync(
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        var results = new Story[ids.Count];

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _hackerNewsOptions.MaxParallelFetches,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(
            Enumerable.Range(0, ids.Count),
            parallelOptions,
            async (i, ct) => results[i] = await _hackerNewsClient.GetStoryAsync(ids[i], ct));

        return results
            .Where(s => s is not null)
            .ToArray();
    }
}