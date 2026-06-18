using System.Net;
using System.Text;
using HackerNews.Infrastructure.Clients.HackerNews;
using Microsoft.Extensions.Logging.Abstractions;

namespace HackerNews.UnitTests.Infrastructure.Clients;

[TestFixture]
public class HackerNewsClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request, cancellationToken));
    }

    private static HackerNewsClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
    {
        var httpClient = new HttpClient(new StubHandler(responder))
        {
            BaseAddress = new Uri("https://example.com/")
        };
        return new HackerNewsClient(httpClient, NullLogger<HackerNewsClient>.Instance);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    [Test]
    public async Task GetBestStoryIdsAsync_DeserializesIds()
    {
        var client = CreateClient((_, _) => Json("[1,2,3]"));

        var ids = await client.GetBestStoryIdsAsync(CancellationToken.None);

        Assert.That(ids, Is.EqualTo(new long[] { 1, 2, 3 }));
    }

    [Test]
    public async Task GetStoryAsync_DeserializesStory()
    {
        var client = CreateClient((_, _) => Json(
            """{"id":1,"title":"Hello","url":"https://x.com","by":"bob","time":1000,"score":42,"descendants":7,"type":"story"}"""));

        var story = await client.GetStoryAsync(1, CancellationToken.None);

        Assert.That(story, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(story.Id, Is.EqualTo(1));
            Assert.That(story.Title, Is.EqualTo("Hello"));
            Assert.That(story.By, Is.EqualTo("bob"));
            Assert.That(story.Score, Is.EqualTo(42));
            Assert.That(story.CommentCount, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task GetStoryAsync_MissingDescendants_DefaultsToZero()
    {
        var client = CreateClient((_, _) => Json(
            """{"id":1,"title":"Hello","by":"bob","time":1000,"score":42,"type":"story"}"""));

        var story = await client.GetStoryAsync(1, CancellationToken.None);

        Assert.That(story.CommentCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GetStoryAsync_DeletedStory_ReturnsNull()
    {
        var client = CreateClient((_, _) => Json("""{"id":1,"deleted":true}"""));

        var story = await client.GetStoryAsync(1, CancellationToken.None);

        Assert.That(story, Is.Null);
    }

    [Test]
    public async Task GetStoryAsync_DeadStory_ReturnsNull()
    {
        var client = CreateClient((_, _) => Json("""{"id":1,"dead":true}"""));

        var story = await client.GetStoryAsync(1, CancellationToken.None);

        Assert.That(story, Is.Null);
    }

    [Test]
    public async Task GetStoryAsync_NullBody_ReturnsNull()
    {
        var client = CreateClient((_, _) => Json("null"));

        var story = await client.GetStoryAsync(1, CancellationToken.None);

        Assert.That(story, Is.Null);
    }

    [Test]
    public async Task GetStoryAsync_HttpError_ReturnsNull()
    {
        var client = CreateClient((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var story = await client.GetStoryAsync(1, CancellationToken.None);

        Assert.That(story, Is.Null);
    }

    [Test]
    public async Task GetStoryAsync_PerRequestTimeout_ReturnsNull()
    {
        var client = CreateClient((_, _) => throw new TaskCanceledException("simulated HttpClient timeout"));

        var story = await client.GetStoryAsync(1, CancellationToken.None);

        Assert.That(story, Is.Null);
    }

    [Test]
    public void GetStoryAsync_CallerCancellation_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var client = CreateClient((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Json("{}");
        });

        Assert.CatchAsync<OperationCanceledException>(
            () => client.GetStoryAsync(1, cts.Token));
    }
}
