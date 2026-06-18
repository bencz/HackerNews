using HackerNews.Application.Abstractions;
using HackerNews.Application.Configuration;
using HackerNews.Application.Exceptions;
using HackerNews.Application.Services;
using HackerNews.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HackerNews.UnitTests.Application.Services;

[TestFixture]
public class BestStoriesServiceTests
{
    private IHackerNewsClient _client;
    private IBestStoriesCacheReader _cacheReader;
    private IOptions<HackerNewsOptions> _options;
    private ILogger<BestStoriesService> _logger;
    private BestStoriesService _service;

    private static Story CreateStory(long id, int score = 100) =>
        new(id, "Title", "https://example.com", "author", DateTimeOffset.UnixEpoch, score, 0);

    private BestStoriesService CreateService(double maxFailureRatio = 0.25, int snapshotSize = 200)
    {
        _options = Options.Create(new HackerNewsOptions
        {
            SnapshotSize = snapshotSize,
            MaxParallelFetches = 5,
            MaxFailureRatio = maxFailureRatio
        });
        return new BestStoriesService(_client, _cacheReader, _options, _logger);
    }

    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IHackerNewsClient>();
        _cacheReader = Substitute.For<IBestStoriesCacheReader>();
        _logger = Substitute.For<ILogger<BestStoriesService>>();
        _service = CreateService();
    }

    [Test]
    public void GetBestStories_CacheNotReady_ThrowsStoriesCacheNotReadyException()
    {
        _cacheReader.IsReady.Returns(false);

        Assert.Throws<StoriesCacheNotReadyException>(() => _service.GetBestStories(10));
    }

    [Test]
    public void GetBestStories_InvalidCount_ThrowsValidationException()
    {
        _cacheReader.IsReady.Returns(true);

        var ex = Assert.Throws<ValidationException>(() => _service.GetBestStories(0));
        Assert.That(ex.Message, Is.EqualTo("Number of stories must be positive"));
    }

    [Test]
    public void GetBestStories_InvalidCount_CheckedBeforeReadiness()
    {
        _cacheReader.IsReady.Returns(false);

        Assert.Throws<ValidationException>(() => _service.GetBestStories(-1));
    }

    [Test]
    public void GetBestStories_RequestLessThanAvailable_ReturnsRequestedCount()
    {
        var stories = new List<Story> { CreateStory(1), CreateStory(2), CreateStory(3) };
        _cacheReader.IsReady.Returns(true);
        _cacheReader.Current.Returns(stories);

        var result = _service.GetBestStories(2);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void GetBestStories_RequestMoreOrEqualToAvailable_ReturnsSameInstance()
    {
        var stories = (IReadOnlyList<Story>)new List<Story> { CreateStory(1) };
        _cacheReader.IsReady.Returns(true);
        _cacheReader.Current.Returns(stories);

        var result = _service.GetBestStories(100);

        Assert.That(result, Is.SameAs(stories));
    }

    [Test]
    public async Task RefreshAsync_EmptyIds_ReturnsNull()
    {
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<long>>([]));

        var result = await _service.RefreshAsync(CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task RefreshAsync_ReturnsStoriesSortedByScoreDescending()
    {
        var ids = (IReadOnlyList<long>)new long[] { 1, 2, 3 };
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ids));
        _client.GetStoryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateStory(1, score: 10)));
        _client.GetStoryAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateStory(2, score: 300)));
        _client.GetStoryAsync(3, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateStory(3, score: 50)));

        var result = await _service.RefreshAsync(CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Score, Is.EqualTo(300));
            Assert.That(result[1].Score, Is.EqualTo(50));
            Assert.That(result[2].Score, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task RefreshAsync_SnapshotSizeLimitsIdsFetched()
    {
        _service = CreateService(snapshotSize: 2);

        var ids = (IReadOnlyList<long>)new long[] { 1, 2, 3, 4, 5 };
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ids));
        _client.GetStoryAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(CreateStory(ci.Arg<long>())));

        await _service.RefreshAsync(CancellationToken.None);

        await _client.DidNotReceive().GetStoryAsync(3, Arg.Any<CancellationToken>());
        await _client.DidNotReceive().GetStoryAsync(4, Arg.Any<CancellationToken>());
        await _client.DidNotReceive().GetStoryAsync(5, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RefreshAsync_NullStoriesAreFiltered()
    {
        _service = CreateService(maxFailureRatio: 1.0);

        var ids = (IReadOnlyList<long>)new long[] { 1, 2, 3 };
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ids));
        _client.GetStoryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateStory(1)));
        _client.GetStoryAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Story>(null));
        _client.GetStoryAsync(3, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateStory(3)));

        var result = await _service.RefreshAsync(CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void RefreshAsync_FailureRatioExceeded_Throws()
    {
        _service = CreateService(maxFailureRatio: 0.25);

        var ids = (IReadOnlyList<long>)new long[] { 1, 2, 3, 4 };
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ids));
        _client.GetStoryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateStory(1)));
        _client.GetStoryAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateStory(2)));
        _client.GetStoryAsync(3, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Story>(null));
        _client.GetStoryAsync(4, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Story>(null));

        Assert.ThrowsAsync<RefreshFailedException>(() => _service.RefreshAsync(CancellationToken.None));
    }

    [Test]
    public async Task RefreshAsync_FailuresWithinThreshold_ReturnsStories()
    {
        _service = CreateService(maxFailureRatio: 0.25);

        var ids = (IReadOnlyList<long>)new long[] { 1, 2, 3, 4 };
        _client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ids));
        _client.GetStoryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateStory(1)));
        _client.GetStoryAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateStory(2)));
        _client.GetStoryAsync(3, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateStory(3)));
        _client.GetStoryAsync(4, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Story>(null));

        var result = await _service.RefreshAsync(CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(3));
    }
}
