using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YouTubester.Application.Channels;
using YouTubester.Persistence.Channels;

namespace YouTubester.Api;

[ApiController]
[Route("api/channels")]
[Tags("Channels")]
[Authorize]
public sealed class ChannelController(
    IChannelSyncService channelSyncService,
    IChannelRepository channelRepository
) : ControllerBase
{
    [HttpPost("sync/{channelName}")]
    [ProducesResponseType(typeof(ChannelSyncResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChannelSyncResult>> Sync([FromRoute] string channelName, CancellationToken ct)
    {
        var channel = await channelRepository.GetChannelByNameAsync(channelName, ct);
        if (channel is null)
        {
            return NotFound(new { message = $"Channel '{channelName}' not found." });
        }

        var result = await channelSyncService.SyncAsync(channel.ChannelId, ct);
        return Ok(result);
    }
}