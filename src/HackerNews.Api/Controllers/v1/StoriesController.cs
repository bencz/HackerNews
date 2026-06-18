using Asp.Versioning;
using HackerNews.Api.Mappers;
using HackerNews.Api.Models;
using HackerNews.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace HackerNews.Api.Controllers.v1;

[ApiController]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
public class StoriesController : ControllerBase
{
    private readonly IBestStoriesService _bestStoriesService;

    public StoriesController(
        IBestStoriesService bestStoriesService)
    {
        _bestStoriesService = bestStoriesService;
    }
    
    [HttpGet("best")]
    [ProducesResponseType(typeof(IEnumerable<StoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<IEnumerable<StoryResponse>> GetBestStories(
        [FromQuery] int? n)
    {
        var bestStories = _bestStoriesService
            .GetBestStories(n)
            .Select(s => s.ToResponse());

        return Ok(bestStories);
    }
}