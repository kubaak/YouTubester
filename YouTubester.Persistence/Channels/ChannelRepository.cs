using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;

namespace YouTubester.Persistence.Channels;

public sealed class ChannelRepository(YouTubesterDb db) : IChannelRepository
{
    public async Task<List<Channel>> GetChannelsAsync(CancellationToken cancellationToken)
    {
        return await db.Set<Channel>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Channel?> GetChannelAsync(string channelId, CancellationToken cancellationToken)
    {
        return await db.Set<Channel>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ChannelId == channelId, cancellationToken);
    }

    public async Task<Channel?> GetChannelByNameAsync(string channelName, CancellationToken cancellationToken)
    {
        return await db.Set<Channel>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == channelName, cancellationToken);
    }
}