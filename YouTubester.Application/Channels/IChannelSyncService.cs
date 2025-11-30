using YouTubester.Application.Contracts.Channels;
using YouTubester.Domain;

namespace YouTubester.Application.Channels;

public interface IChannelSyncService
{
    Task<Channel> PullChannelAsync(string userId, string channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Synchronizes a single channel for the specified user.
    /// </summary>
    Task<ChannelSyncResult> SyncChannelAsync(string userId, string channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns all YouTube channels available to pull for the specified user.
    /// </summary>
    Task<IReadOnlyList<AvailableChannelDto>> GetAvailableYoutubeChannelsForUserAsync(
        string userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Syncs all channels owned by the specified user.
    /// Intended to be used from a background job where only the user id is known.
    /// </summary>
    Task SyncChannelsForUserAsync(string userId, CancellationToken cancellationToken = default);
}
