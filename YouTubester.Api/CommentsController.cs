using YouTubester.Application;
using YouTubester.Application.Contracts;

namespace YouTubester.Api;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class CommentsController(ICommentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDrafts(CancellationToken cancellationToken = default) 
        => Ok(await service.GetDraftsAsync(cancellationToken));

    [HttpDelete]
    public async Task<IActionResult> DeleteDraft(string id, CancellationToken cancellationToken)
    {
        await service.GeDeleteAsync(id, cancellationToken);
        return Ok();
    }
    
    [HttpPost("approve")]
    public async Task<ActionResult<BatchDecisionResultDto>> BatchApprove(
        [FromBody] IEnumerable<DraftDecisionDto> decisions,
        CancellationToken cancellationToken)
    {
        if (decisions is null) return BadRequest("Missing decisions.");
        var result = await service.ApplyBatchAsync(decisions, cancellationToken);
        return Ok(result);
    }
}