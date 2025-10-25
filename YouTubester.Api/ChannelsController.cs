using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YouTubester.Application.Channels;
using YouTubester.Domain;

namespace YouTubester.Api;

/// <summary>Channels</summary>
[ApiController]
[Route("api/channels")]
[Tags("Channels")]
[Authorize]
public sealed class ChannelsController(IChannelSyncService channelSyncService) : ControllerBase
{
    /// <summary>
    /// Pulls channel metadata from YouTube (by name)
    /// </summary>
    /// <param name="channelName">Channel name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The persisted <see cref="Channel"/>.</returns>
    [HttpPost("pull/{channelName}")]
    [ProducesResponseType(typeof(Channel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Channel>> PullAsync([FromRoute] string channelName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return ValidationProblem("'channelName' is required.");
        }

        var channel = await channelSyncService.PullChannelAsync(channelName, ct);
        return Ok(channel);
    }


    /// <summary>Runs full sync for a channel by name.</summary>
    [HttpPost("sync/{channelName}")]
    [ProducesResponseType(typeof(ChannelSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChannelSyncResult>> SyncAsync([FromRoute] string channelName, CancellationToken ct)
    {
        var result = await channelSyncService.SyncByNameAsync(channelName, ct);
        return Ok(result);
    }
}