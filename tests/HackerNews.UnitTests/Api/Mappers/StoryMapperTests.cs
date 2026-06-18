using HackerNews.Api.Mappers;
using HackerNews.Domain;

namespace HackerNews.UnitTests.Api.Mappers;

[TestFixture]
public class StoryMapperTests
{
    [Test]
    public void ToResponse_MapsAllFieldsCorrectly()
    {
        var time = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var story = new Story(42, "Title", "https://example.com", "author", time, 150, 30);

        var response = story.ToResponse();

        Assert.Multiple(() =>
        {
            Assert.That(response.Title, Is.EqualTo("Title"));
            Assert.That(response.Uri, Is.EqualTo("https://example.com"));
            Assert.That(response.PostedBy, Is.EqualTo("author"));
            Assert.That(response.Time, Is.EqualTo(time));
            Assert.That(response.Score, Is.EqualTo(150));
            Assert.That(response.CommentCount, Is.EqualTo(30));
        });
    }
}
