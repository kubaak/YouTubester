using YouTubester.Application;

namespace YouTubester.Api;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class CommentsController(ICommentService service) : ControllerBase
{
    [HttpGet("drafts")]
    public async Task<IActionResult> GetDrafts()
        => Ok(await service.GetDraftsAsync());

    [HttpPost("{commentId}/approve")]
    public async Task<IActionResult> Approve(string commentId)
    {
        await service.ApproveDraftAsync(commentId);
        return Ok();
    }

    [HttpPost("{commentId}/post")]
    public async Task<IActionResult> PostReply(string commentId)
    {
        await service.PostReplyAsync(commentId);
        return Ok();
    }
}