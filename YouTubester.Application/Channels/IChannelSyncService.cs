using YouTubester.Domain;

namespace YouTubester.Application.Channels;

public interface IChannelSyncService
{
    Task<Channel> PullChannelAsync(string userId, string channelName, CancellationToken cancellationToken);

    /// <summary>
    /// Syncs all channels owned by the specified user.
    /// Intended to be used from a background job where only the user id is known.
    /// </summary>
    Task SyncChannelsForUserAsync(string userId, CancellationToken cancellationToken = default);
}