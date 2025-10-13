using YouTubester.Application;
using YouTubester.Application.Contracts;
using YouTubester.Domain;

namespace YouTubester.Api;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/replies")]
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
        [FromBody] IEnumerable<DraftDecisionDto> decisions,
        CancellationToken cancellationToken)
    {
        if (decisions is null) return BadRequest("Missing decisions.");
        var result = await service.ApplyBatchAsync(decisions, cancellationToken);
        return Ok(result);
    }

    [HttpPut("ignore/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ignore([FromRoute] string id, CancellationToken cancellationToken)
    {
        var reply = await service.IgnoreAsync(id, cancellationToken);
        if (reply is null) return NotFound();
        
        return Ok(reply);
    }
}