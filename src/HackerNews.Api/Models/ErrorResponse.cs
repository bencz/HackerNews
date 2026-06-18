namespace HackerNews.Api.Models;

public class ErrorResponse
{
    public string Message { get; init; }
    public string CorrelationId { get; init; }
}