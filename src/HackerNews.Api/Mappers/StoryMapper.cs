using HackerNews.Api.Models;
using HackerNews.Domain;

namespace HackerNews.Api.Mappers;

public static class StoryMapper
{
    public static StoryResponse ToResponse(this Story story) => new(
        Title: story.Title,
        Uri: story.Url,
        PostedBy: story.By,
        Time: story.Time,
        Score: story.Score,
        CommentCount: story.CommentCount);
}
