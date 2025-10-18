using Hangfire;
using Microsoft.AspNetCore.Mvc;
using YouTubester.Application;
using YouTubester.Application.Contracts.Videos;
using YouTubester.Application.Jobs;

namespace YouTubester.Api;

[ApiController]
[Route("api/videos")]
[Tags("Videos")]
public sealed class VideosController(
    IBackgroundJobClient  jobClient,
    IVideoService service
    ) : ControllerBase
{
    [HttpPost("copy-template")]
    public IActionResult CopyTemplate(
        [FromBody] CopyVideoTemplateRequest request)
    {
        var res = jobClient.Enqueue<CopyVideoTemplateJob>(j => j.Run(request, JobCancellationToken.Null));
        return Ok(res);
    }
    
    [HttpPost("sync")]
    [ProducesResponseType(typeof(SyncVideosResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncVideosResult>> Sync(CancellationToken ct)
        => Ok(await service.SyncChannelVideosAsync(ct));
}