using HackerNews.Api.Configuration.Middlewares;

namespace HackerNews.UnitTests.Api.Configuration.Middlewares;

[TestFixture]
public class RequestLoggerMiddlewareExtensionsTests
{
    [TestCase("/health/ready", true)]
    [TestCase("/health/live", true)]
    [TestCase("/health/startup", true)]
    [TestCase("/api/v1/stories/best", false)]
    [TestCase(null, false)]
    [TestCase("", false)]
    public void ShouldIgnorePath_ReturnsExpected(string path, bool expected)
    {
        Assert.That(RequestLoggerMiddlewareExtensions.ShouldIgnorePath(path), Is.EqualTo(expected));
    }
}
