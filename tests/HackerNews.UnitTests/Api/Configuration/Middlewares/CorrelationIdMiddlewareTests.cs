using HackerNews.Api.Configuration.Middlewares;
using Microsoft.AspNetCore.Http;

namespace HackerNews.UnitTests.Api.Configuration.Middlewares;

[TestFixture]
public class CorrelationIdMiddlewareTests
{
    [Test]
    public async Task Invoke_NoHeader_GeneratesNewGuid()
    {
        string capturedId = null;
        var middleware = new CorrelationIdMiddleware(ctx =>
        {
            capturedId = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        });

        await middleware.Invoke(new DefaultHttpContext());

        Assert.That(Guid.TryParse(capturedId, out _), Is.True);
    }

    [Test]
    public async Task Invoke_WithHeader_UsesProvidedValue()
    {
        string capturedId = null;
        var middleware = new CorrelationIdMiddleware(ctx =>
        {
            capturedId = ctx.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Headers["x-correlation-id"] = "my-custom-id";

        await middleware.Invoke(context);

        Assert.That(capturedId, Is.EqualTo("my-custom-id"));
    }
}
