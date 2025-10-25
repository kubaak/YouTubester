using Microsoft.AspNetCore.Mvc;
using YouTubester.Application.Channels;
using YouTubester.Persistence.Channels;

namespace YouTubester.Api;

/// <summary>Channels</summary>
[ApiController]
[Route("api/channels")]
[Tags("Channels")]
public sealed class ChannelController(IChannelSyncService channelSyncService) : ControllerBase
{
    /// <summary>Runs full sync for a channel by name.</summary>
    [HttpPost("sync/{channelName}")]
    [ProducesResponseType(typeof(ChannelSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChannelSyncResult>> SyncAsync([FromRoute] string channelName, CancellationToken ct)
    {
        var result = await channelSyncService.SyncByNameAsync(channelName, ct); // service loads the channel ONCE
        return Ok(result);
    }
}