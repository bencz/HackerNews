using HackerNews.Api.Models;
using HackerNews.Application.Exceptions;

namespace HackerNews.Api.Configuration.Middlewares;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    private const int ClientClosedRequest = 499;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = ClientClosedRequest;
        }
        catch (Exception ex)
        {
            var statusCode = MapStatusCode(ex);
            var correlationId = context.Items["CorrelationId"] as string;

            if (statusCode == StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

            var response = new ErrorResponse
            {
                Message = statusCode == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred"
                    : ex.Message,
                CorrelationId = correlationId
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(response);
        }
    }

    private static int MapStatusCode(Exception ex) =>
        ex switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            StoriesCacheNotReadyException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
}
