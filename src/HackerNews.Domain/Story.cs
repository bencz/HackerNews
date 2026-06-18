namespace HackerNews.Domain;

public sealed record Story(
    long Id,
    string Title,
    string Url,
    string By,
    DateTimeOffset Time,
    int Score,
    int CommentCount);
