using YouTubester.Domain;

namespace YouTubester.Persistence.Channels;

public interface IChannelRepository
{
    public Task<List<Channel>> GetChannelsAsync(CancellationToken cancellationToken);
    public Task<List<Channel>> GetChannelsForUserAsync(string userId, CancellationToken cancellationToken);
    public Task<Channel?> GetChannelAsync(string channelId, CancellationToken cancellationToken);
    public Task SetUploadsCutoffAsync(string channelId, DateTimeOffset cutoff, CancellationToken cancellationToken);
    public Task UpsertChannelAsync(Channel channel, CancellationToken cancellationToken);
}