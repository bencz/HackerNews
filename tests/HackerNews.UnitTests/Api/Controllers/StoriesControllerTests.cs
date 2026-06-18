using HackerNews.Api.Controllers.v1;
using HackerNews.Api.Models;
using HackerNews.Application.Abstractions;
using HackerNews.Domain;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace HackerNews.UnitTests.Api.Controllers;

[TestFixture]
public class StoriesControllerTests
{
    private IBestStoriesService _bestStoriesService;
    private StoriesController _controller;

    [SetUp]
    public void SetUp()
    {
        _bestStoriesService = Substitute.For<IBestStoriesService>();
        _controller = new StoriesController(_bestStoriesService);
    }

    [Test]
    public void GetBestStories_ReturnsOkWithMappedResponses()
    {
        var stories = new List<Story>
        {
            new(1, "Story 1", "https://one.com", "user1", DateTimeOffset.UnixEpoch, 100, 10),
            new(2, "Story 2", "https://two.com", "user2", DateTimeOffset.UnixEpoch, 200, 20)
        };
        _bestStoriesService.GetBestStories(2).Returns(stories);

        var result = _controller.GetBestStories(2);

        var okResult = result.Result as OkObjectResult;
        var responses = (okResult?.Value as IEnumerable<StoryResponse>)?.ToList();

        Assert.That(responses, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(responses, Has.Count.EqualTo(2));
            Assert.That(responses[0].Title, Is.EqualTo("Story 1"));
            Assert.That(responses[0].Uri, Is.EqualTo("https://one.com"));
            Assert.That(responses[0].PostedBy, Is.EqualTo("user1"));
            Assert.That(responses[1].Title, Is.EqualTo("Story 2"));
        });
    }
}
