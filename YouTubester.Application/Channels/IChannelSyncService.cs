namespace YouTubester.Application.Channels;

public interface IChannelSyncService
{
    /// <summary>
    /// Runs the full sync (uploads delta + playlist membership) for the given channel name.
    /// Throws NotFoundException if channel does not exist.
    /// </summary>
    Task<ChannelSyncResult> SyncByNameAsync(string channelName, CancellationToken ct);
}