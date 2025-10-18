using Hangfire;
using Microsoft.AspNetCore.Mvc;
using YouTubester.Application;
using YouTubester.Application.Contracts.Videos;
using YouTubester.Application.Jobs;
using YouTubester.Persistence.Channels;

namespace YouTubester.Api;

[ApiController]
[Route("api/videos")]
[Tags("Videos")]
public sealed class VideosController(
    IBackgroundJobClient  jobClient,
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
    public async Task<ActionResult<SyncVideosResult>> Sync([FromRoute]string channelName, CancellationToken ct)
    {
        var channel = await channelRepository.GetChannelByNameAsync(channelName, ct);
        if (channel is null)
        {
            return NotFound(new { message = $"Channel '{channelName}' not found." });
        }

        var result = await service.SyncChannelVideosAsync(channel.UploadsPlaylistId, ct);
        return Ok(result);
    }
}