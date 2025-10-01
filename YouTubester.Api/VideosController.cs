using Hangfire;
using Microsoft.AspNetCore.Mvc;
using YouTubester.Application;
using YouTubester.Application.Jobs;

namespace YouTubester.Api;

[ApiController]
[Route("api/videos")]
public sealed class VideosController(IBackgroundJobClient  jobClient) : ControllerBase
{
    [HttpPost("copy-template")]
    public IActionResult CopyTemplate(
        [FromBody] CopyVideoTemplateRequest request)
    {
        var res = jobClient.Enqueue<CopyVideoTemplateJob>(j => j.Run(request, JobCancellationToken.Null));
        return Ok(res);
    }
}