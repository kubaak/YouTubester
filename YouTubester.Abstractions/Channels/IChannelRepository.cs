using YouTubester.Domain;

namespace YouTubester.Abstractions.Channels;

public interface IChannelRepository
{
    Task<List<Channel>> GetChannelsAsync(CancellationToken cancellationToken);

    Task<List<Channel>> GetChannelsForUserAsync(string userId, CancellationToken cancellationToken);

    Task<Channel?> GetChannelAsync(string channelId, CancellationToken cancellationToken);

    Task SetUploadsCutoffAsync(string channelId, DateTimeOffset cutoff, CancellationToken cancellationToken);

    Task UpsertChannelAsync(Channel channel, CancellationToken cancellationToken);
}