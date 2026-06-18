namespace HackerNews.Application.Exceptions;

public sealed class RefreshFailedException(string message) : AppException(message);
