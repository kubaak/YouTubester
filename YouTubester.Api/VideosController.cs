using Hangfire;
using Microsoft.AspNetCore.Mvc;
using YouTubester.Application;
using YouTubester.Application.Contracts;
using YouTubester.Application.Contracts.Videos;
using YouTubester.Application.Exceptions;
using YouTubester.Application.Jobs;
using YouTubester.Domain;
using YouTubester.Persistence.Channels;

namespace YouTubester.Api;

[ApiController]
[Route("api/videos")]
[Tags("Videos")]
public sealed class VideosController(
    IBackgroundJobClient jobClient,
    IVideoService service,
    IChannelRepository channelRepository
    ) : ControllerBase
{
    [HttpPost("copy-template")]
    public IActionResult CopyTemplate(
        [FromBody] CopyVideoTemplateRequest request)
    {
        var res = jobClient.Enqueue<CopyVideoTemplateJob>(j => j.Run(request, JobCancellationToken.Null));
        return Ok(res);
    }

    [HttpPost("sync/{channelName}")]
    [ProducesResponseType(typeof(SyncVideosResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncVideosResult>> Sync([FromRoute] string channelName, CancellationToken ct)
    {
        var channel = await channelRepository.GetChannelByNameAsync(channelName, ct);
        if (channel is null)
        {
            return NotFound(new { message = $"Channel '{channelName}' not found." });
        }

        var result = await service.SyncChannelVideosAsync(channel.UploadsPlaylistId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets a paginated list of videos with optional title filtering and visibility filtering.
    /// </summary>
    /// <param name="title">Optional case-insensitive substring filter for video titles.</param>
    /// <param name="visibility">Optional visibility filter array. Multiple values can be passed as repeated keys (?visibility=Public&amp;visibility=Unlisted) or comma-separated (?visibility=Public,Unlisted). Values are case-insensitive. Valid options: Public, Unlisted, Private, Scheduled.</param>
    /// <param name="pageSize">Number of items per page (1-100, defaults to 30).</param>
    /// <param name="pageToken">Cursor token for pagination, or null for first page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated result containing videos and optional next page token.</returns>
    /// <response code="200">Returns the paginated list of videos.</response>
    /// <response code="400">Invalid pageSize, pageToken, or visibility values provided.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VideoListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<VideoListItemDto>>> GetVideos(
        [FromQuery] string? title,
        [FromQuery(Name = "visibility")] VideoVisibility[]? visibility,
        [FromQuery] int? pageSize,
        [FromQuery] string? pageToken,
        CancellationToken ct)
    {
        try
        {
            var result = await service.GetVideosAsync(title, visibility, pageSize, pageToken, ct);
            return Ok(result);
        }
        catch (InvalidPageSizeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidPageTokenException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
