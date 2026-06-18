using System.Net;
using System.Text.Json;
using HackerNews.Api.Configuration.Middlewares;
using HackerNews.Api.Models;
using HackerNews.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace HackerNews.UnitTests.Api.Configuration.Middlewares;

[TestFixture]
public class ExceptionMiddlewareTests
{
    private static ExceptionMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, Substitute.For<ILogger<ExceptionMiddleware>>());

    private static HttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ErrorResponse> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ErrorResponse>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    [Test]
    public async Task Invoke_ValidationException_Returns400()
    {
        var middleware = CreateMiddleware(_ => throw new ValidationException("bad input"));
        var context = CreateContext();

        await middleware.Invoke(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Invoke_StoriesCacheNotReadyException_Returns503()
    {
        var middleware = CreateMiddleware(_ => throw new StoriesCacheNotReadyException());
        var context = CreateContext();

        await middleware.Invoke(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo((int)HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task Invoke_UnhandledException_Returns500()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("boom"));
        var context = CreateContext();

        await middleware.Invoke(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo((int)HttpStatusCode.InternalServerError));
    }

    [Test]
    public async Task Invoke_UnhandledException_DoesNotLeakExceptionMessage()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("secret detail"));
        var context = CreateContext();

        await middleware.Invoke(context);
        var body = await ReadResponseBody(context);

        Assert.That(body.Message, Is.EqualTo("An unexpected error occurred"));
    }

    [Test]
    public async Task Invoke_Exception_WritesErrorResponseWithMessageAndCorrelationId()
    {
        var middleware = CreateMiddleware(_ => throw new ValidationException("field is required"));
        var context = CreateContext();
        context.Items["CorrelationId"] = "test-123";

        await middleware.Invoke(context);
        var body = await ReadResponseBody(context);

        Assert.Multiple(() =>
        {
            Assert.That(body.Message, Is.EqualTo("field is required"));
            Assert.That(body.CorrelationId, Is.EqualTo("test-123"));
        });
    }
}
