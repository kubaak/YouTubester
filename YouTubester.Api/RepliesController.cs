using YouTubester.Application;
using YouTubester.Application.Contracts;
using YouTubester.Application.Contracts.Replies;
using YouTubester.Domain;

namespace YouTubester.Api;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/replies")]
[Tags("Replies")]
public class RepliesController(IReplyService service) : ControllerBase
{
    [HttpGet]
    public async  Task<ActionResult<IEnumerable<Reply>>> GetDrafts(CancellationToken cancellationToken = default) 
        => Ok(await service.GetRepliesForApprovalAsync(cancellationToken));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDraft([FromRoute] string id, CancellationToken cancellationToken)
    {
        var reply = await service.DeleteAsync(id, cancellationToken);
        if (reply is null) return NotFound();
        return Ok(reply);
    }
    
    [HttpPost("approve")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BatchDecisionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<BatchDecisionResultDto>> BatchApprove(
        [FromBody] DraftDecisionDto[] decisions,
        CancellationToken cancellationToken)
    {
        if (decisions.Length == 0) return BadRequest("Missing decisions.");
        var result = await service.ApplyBatchAsync(decisions, cancellationToken);
        return Ok(result);
    }
    
    [HttpPost("batch-ignore")]
    [ProducesResponseType(typeof(BatchIgnoreResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchIgnoreResult>> BatchIgnore(
        [FromBody] string[] commentIds,
        CancellationToken ct)
    {
        if (commentIds.Length == 0)
            return BadRequest(new { error = "CommentIds cannot be empty." });

        var result = await service.IgnoreBatchAsync(commentIds, ct);
        return Ok(result);
    }
}