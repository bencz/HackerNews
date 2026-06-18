namespace HackerNews.Application.Exceptions;

public abstract class AppException(string message) : Exception(message);