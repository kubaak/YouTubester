using System.Security.Claims;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YouTubester.Application.Channels;
using YouTubester.Application.Contracts.Channels;
using YouTubester.Domain;

namespace YouTubester.Api;

/// <summary>Channels</summary>
[ApiController]
[Route("api/channels")]
[Tags("Channels")]
[Authorize]
public sealed class ChannelsController(
    IChannelSyncService channelSyncService,
    IBackgroundJobClient backgroundJobClient)
    : ControllerBase
{
    /// <summary>
    /// Returns all YouTube channels available to pull for the current user.
    /// </summary>
    [HttpGet("available")]
    [ProducesResponseType(typeof(IReadOnlyList<AvailableChannelDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AvailableChannelDto>>> GetAvailableChannelsAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var channels = await channelSyncService.GetAvailableYoutubeChannelsForUserAsync(userId, cancellationToken);
        return Ok(channels);
    }

    /// <summary>
    /// Pulls channel metadata from YouTube (by name)
    /// </summary>
    /// <param name="channelName">Channel name</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The persisted <see cref="Channel"/>.</returns>
    [HttpPost("pull/{channelName}")]
    [ProducesResponseType(typeof(Channel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Channel>> PullAsync([FromRoute] string channelName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return ValidationProblem("'channelName' is required.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var channel = await channelSyncService.PullChannelAsync(userId, channelName, cancellationToken);
        return Ok(channel);
    }

    /// <summary>
    /// Schedules a background sync of all channels owned by the currently signed-in user.
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult SyncCurrentUsersChannels()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        backgroundJobClient.Enqueue<IChannelSyncService>(
            service => service.SyncChannelsForUserAsync(userId, CancellationToken.None));

        return Accepted(new { status = "scheduled" });
    }
}