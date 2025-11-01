using YouTubester.Domain;

namespace YouTubester.Application.Channels;

public interface IChannelSyncService
{
    Task<Channel> PullChannelAsync(string channelName, CancellationToken ct);

    /// <summary>
    /// Runs the full sync (uploads delta + playlist membership) for the given channel name.
    /// Throws NotFoundException if channel does not exist.
    /// </summary>
    Task<ChannelSyncResult> SyncByNameAsync(string channelName, CancellationToken ct);
}