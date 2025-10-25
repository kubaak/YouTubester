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

    public async Task SetUploadsCutoffAsync(string channelId, DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var channel = await db.Set<Channel>().FirstOrDefaultAsync(c => c.ChannelId == channelId, cancellationToken);
        if (channel is null)
        {
            return;
        }

        channel.LastUploadsCutoff = cutoff;
        channel.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertChannelAsync(Channel channel, CancellationToken cancellationToken)
    {
        var existingChannel = await db.Set<Channel>()
            .FirstOrDefaultAsync(c => c.ChannelId == channel.ChannelId, cancellationToken);
        if (existingChannel != null)
        {
            existingChannel.Name = channel.Name;
            existingChannel.UploadsPlaylistId = channel.UploadsPlaylistId;
            existingChannel.ETag = channel.ETag;
            existingChannel.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.Set<Channel>().Add(channel);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}