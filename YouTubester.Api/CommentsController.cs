using YouTubester.Application;
using YouTubester.Application.Contracts;

namespace YouTubester.Api;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class CommentsController(ICommentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDrafts() => Ok(await service.GetDraftsAsync());
    
    [HttpPost("approve")]
    public async Task<ActionResult<BatchDecisionResultDto>> BatchApprove(
        [FromBody] IEnumerable<DraftDecisionDto> decisions,
        CancellationToken ct)
    {
        if (decisions is null) return BadRequest("Missing decisions.");
        var result = await service.ApplyBatchAsync(decisions, ct);
        return Ok(result);
    }
}