namespace HackerNews.Application.Exceptions;

public class ValidationException(string message) : AppException(message);