using YouTubester.Domain;

namespace YouTubester.Persistence.Channels;

public interface IChannelRepository
{
    public Task<List<Channel>> GetChannelsAsync(CancellationToken cancellationToken);
    public Task<Channel?> GetChannelAsync(string channelId, CancellationToken cancellationToken);
    public Task<Channel?> GetChannelByNameAsync(string name, CancellationToken cancellationToken);
}