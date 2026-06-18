using HackerNews.Application.Cache;
using HackerNews.Domain;

namespace HackerNews.UnitTests.Application.Cache;

[TestFixture]
public class BestStoriesCacheTests
{
    private BestStoriesCache _cache;

    private static Story CreateStory(long id, int score = 100) =>
        new(id, "Title", "https://example.com", "author", DateTimeOffset.UnixEpoch, score, 0);

    [SetUp]
    public void SetUp()
    {
        _cache = new BestStoriesCache();
    }

    [Test]
    public void InitialState_IsNotReady()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_cache.IsReady, Is.False);
            Assert.That(_cache.Current, Is.Empty);
            Assert.That(_cache.Version, Is.Zero);
        });
    }

    [Test]
    public void TryUpdate_NewerVersion_AppliesAndAdvancesVersion()
    {
        var applied = _cache.TryUpdate(1, [CreateStory(1), CreateStory(2)]);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.True);
            Assert.That(_cache.IsReady, Is.True);
            Assert.That(_cache.Current, Has.Count.EqualTo(2));
            Assert.That(_cache.Version, Is.EqualTo(1));
        });
    }

    [Test]
    public void TryUpdate_OlderVersion_Ignored()
    {
        _cache.TryUpdate(5, [CreateStory(1)]);

        var applied = _cache.TryUpdate(3, [CreateStory(2), CreateStory(3)]);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.False);
            Assert.That(_cache.Version, Is.EqualTo(5));
            Assert.That(_cache.Current, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TryUpdate_EqualVersion_Ignored()
    {
        _cache.TryUpdate(5, [CreateStory(1)]);

        var applied = _cache.TryUpdate(5, [CreateStory(2), CreateStory(3)]);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.False);
            Assert.That(_cache.Current, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TryUpdate_CreatesImmutableSnapshot()
    {
        var stories = new List<Story> { CreateStory(1) };

        _cache.TryUpdate(1, stories);
        stories.Add(CreateStory(2));

        Assert.That(_cache.Current, Has.Count.EqualTo(1));
    }

    [Test]
    public void TryUpdate_NewerVersion_ReflectsLatestData()
    {
        _cache.TryUpdate(1, [CreateStory(1)]);
        _cache.TryUpdate(2, [CreateStory(2), CreateStory(3)]);

        Assert.Multiple(() =>
        {
            Assert.That(_cache.Current, Has.Count.EqualTo(2));
            Assert.That(_cache.Version, Is.EqualTo(2));
        });
    }

    [Test]
    public void ConcurrentReads_WhileWriting_NeverObserveTornSnapshot()
    {
        const int writerIterations = 50_000;
        const int readerCount = 4;
        var violations = 0;

        using var cts = new CancellationTokenSource();

        var readers = Enumerable.Range(0, readerCount).Select(_ => Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var isReady = _cache.IsReady;
                var stories = _cache.Current;

                if (isReady && stories.Count == 0)
                    Interlocked.Increment(ref violations);
            }
        })).ToArray();

        for (var i = 1; i <= writerIterations; i++)
            _cache.TryUpdate(i, [CreateStory(i)]);

        cts.Cancel();
        Task.WaitAll(readers);

        Assert.That(Volatile.Read(ref violations), Is.Zero,
            "A reader saw IsReady=true with an empty Stories list (torn snapshot)");
    }

    [Test]
    public void ConcurrentReads_WhileWriting_SnapshotCountIsAlwaysConsistent()
    {
        const int writerIterations = 50_000;
        const int readerCount = 4;
        var violations = 0;

        using var cts = new CancellationTokenSource();

        var readers = Enumerable.Range(0, readerCount).Select(_ => Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var stories = _cache.Current;
                var count = stories.Count;

                if (count != 0 && count != 3)
                    Interlocked.Increment(ref violations);
            }
        })).ToArray();

        for (var i = 1; i <= writerIterations; i++)
            _cache.TryUpdate(i, [CreateStory(1), CreateStory(2), CreateStory(3)]);

        cts.Cancel();
        Task.WaitAll(readers);

        Assert.That(Volatile.Read(ref violations), Is.Zero,
            "A reader saw a partially written Stories list (non-atomic snapshot swap)");
    }

    [Test]
    public void ConcurrentReads_WhileWriting_AlwaysSeeMatchingGeneration()
    {
        const int writerIterations = 50_000;
        const int readerCount = 4;
        var violations = 0;

        using var cts = new CancellationTokenSource();

        var readers = Enumerable.Range(0, readerCount).Select(_ => Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var stories = _cache.Current;

                if (stories.Count == 0)
                    continue;

                var firstId = stories[0].Id;
                for (var j = 1; j < stories.Count; j++)
                {
                    if (stories[j].Id != firstId)
                    {
                        Interlocked.Increment(ref violations);
                        break;
                    }
                }
            }
        })).ToArray();

        for (var i = 1; i <= writerIterations; i++)
        {
            var story = CreateStory(i);
            _cache.TryUpdate(i, [story, story, story]);
        }

        cts.Cancel();
        Task.WaitAll(readers);

        Assert.That(Volatile.Read(ref violations), Is.Zero,
            "A reader saw stories from different update generations (non-atomic snapshot)");
    }
}
