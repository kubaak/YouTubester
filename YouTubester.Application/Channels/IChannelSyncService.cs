namespace YouTubester.Application.Channels;

public interface IChannelSyncService
{
    Task<ChannelSyncResult> SyncAsync(string channelId, CancellationToken cancellationToken);
}