using System.Net.Http.Json;
using System.Text.Json;
using HackerNews.Application.Abstractions;
using HackerNews.Domain;
using Microsoft.Extensions.Logging;

namespace HackerNews.Infrastructure.Clients.HackerNews;

internal sealed class HackerNewsClient : IHackerNewsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<HackerNewsClient> _logger;

    public HackerNewsClient(HttpClient httpClient, ILogger<HackerNewsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await _httpClient
            .GetFromJsonAsync<long[]>("v0/beststories.json", JsonOptions, cancellationToken);

        return ids ?? [];
    }

    public async Task<Story> GetStoryAsync(long id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _httpClient
                .GetFromJsonAsync<HackerNewsItemDto>($"v0/item/{id}.json", JsonOptions, cancellationToken);

            return ToStory(dto);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch story {StoryId}", id);
            return null;
        }
    }

    private static Story ToStory(HackerNewsItemDto dto)
    {
        if (dto is null or { Deleted: true } or { Dead: true })
            return null;

        return new Story(
            Id: dto.Id,
            Title: dto.Title ?? string.Empty,
            Url: dto.Url ?? string.Empty,
            By: dto.By ?? string.Empty,
            Time: DateTimeOffset.FromUnixTimeSeconds(dto.Time),
            Score: dto.Score,
            CommentCount: dto.Descendants ?? 0);
    }
}
