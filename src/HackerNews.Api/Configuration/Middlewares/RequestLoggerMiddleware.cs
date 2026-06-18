using System.Collections.Frozen;
using System.Diagnostics;

namespace HackerNews.Api.Configuration.Middlewares;

internal static class RequestLoggerMiddlewareExtensions
{
    private static readonly FrozenSet<string> PathsToIgnore =
    [
        "/health/ready",
        "/health/live",
        "/health/startup"
    ];

    public static bool ShouldIgnorePath(string path) =>
        !string.IsNullOrEmpty(path) && PathsToIgnore.Contains(path);
}

public class RequestLoggerMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<RequestLoggerMiddleware>();

    public async Task Invoke(HttpContext context)
    {
        var path = context.Request.Path.Value;
        var method = context.Request.Method;
        var query = context.Request.QueryString.Value;

        if (RequestLoggerMiddlewareExtensions.ShouldIgnorePath(path))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Starting request {Method} {Path}{Query}",
                method, path, query);

            await next(context);

            stopwatch.Stop();

            _logger.LogInformation(
                "Request completed {Method} {Path}{Query} - Status: {StatusCode} - Elapsed: {Elapsed}s",
                method,
                path,
                query,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Request failed {Method} {Path}{Query} - Elapsed: {Elapsed}s",
                method,
                path,
                query,
                stopwatch.Elapsed.TotalSeconds);

            throw;
        }
    }
}