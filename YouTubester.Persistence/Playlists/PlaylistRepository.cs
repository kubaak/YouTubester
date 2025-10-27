using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;

namespace YouTubester.Persistence.Playlists;

public sealed class PlaylistRepository(YouTubesterDb databaseContext) : IPlaylistRepository
{
    public async Task<List<Playlist>> GetByChannelAsync(string channelId, CancellationToken cancellationToken)
    {
        return await databaseContext.Playlists
            .AsNoTracking()
            .Where(p => p.ChannelId == channelId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Playlist?> GetAsync(string playlistId, CancellationToken cancellationToken)
    {
        return await databaseContext.Playlists
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlaylistId == playlistId, cancellationToken);
    }

    public async Task<int> UpsertAsync(IEnumerable<Playlist> playlists, CancellationToken cancellationToken)
    {
        var playlistList = playlists.ToList();
        if (playlistList.Count == 0)
        {
            return 0;
        }

        var playlistIds = playlistList.Select(p => p.PlaylistId).ToHashSet();

        var existingPlaylists = await databaseContext.Playlists
            .Where(p => playlistIds.Contains(p.PlaylistId))
            .ToDictionaryAsync(p => p.PlaylistId, p => p, cancellationToken);

        var currentTime = DateTimeOffset.UtcNow;
        var insertedCount = 0;
        var updatedCount = 0;

        foreach (var playlist in playlistList)
        {
            if (!existingPlaylists.TryGetValue(playlist.PlaylistId, out var existingPlaylist))
            {
                databaseContext.Playlists.Add(playlist);
                insertedCount++;
            }
            else
            {
                existingPlaylist.UpdateTitle(playlist.Title, currentTime, playlist.ETag);
                updatedCount++;
            }
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        return insertedCount + updatedCount;
    }

    public async Task<HashSet<string>> GetMembershipVideoIdsAsync(string playlistId, CancellationToken cancellationToken)
    {
        var videoIds = await databaseContext.VideoPlaylists
            .AsNoTracking()
            .Where(vp => vp.PlaylistId == playlistId)
            .Select(vp => vp.VideoId)
            .ToListAsync(cancellationToken);

        return videoIds.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<int> AddMembershipsAsync(string playlistId, IEnumerable<string> videoIds, CancellationToken cancellationToken)
    {
        var candidateVideoIds = videoIds.ToHashSet(StringComparer.Ordinal);
        if (candidateVideoIds.Count == 0)
        {
            return 0;
        }

        // Get existing memberships for this playlist
        var existingMemberships = await databaseContext.VideoPlaylists
            .AsNoTracking()
            .Where(vp => vp.PlaylistId == playlistId && candidateVideoIds.Contains(vp.VideoId))
            .Select(vp => vp.VideoId)
            .ToHashSetAsync(cancellationToken);

        // Filter to new memberships only
        var newVideoIds = candidateVideoIds.Except(existingMemberships).ToList();
        if (newVideoIds.Count == 0)
        {
            return 0;
        }

        // Filter to video IDs that exist in Videos table (FK safety)
        var existingVideoIds = await databaseContext.Videos
            .AsNoTracking()
            .Where(v => newVideoIds.Contains(v.VideoId))
            .Select(v => v.VideoId)
            .ToHashSetAsync(cancellationToken);

        var validNewVideoIds = newVideoIds.Where(vid => existingVideoIds.Contains(vid)).ToList();
        if (validNewVideoIds.Count == 0)
        {
            return 0;
        }

        // Add memberships in batches
        const int batchSize = 500;
        var addedCount = 0;

        for (var i = 0; i < validNewVideoIds.Count; i += batchSize)
        {
            var batch = validNewVideoIds.Skip(i).Take(batchSize);
            var memberships = batch.Select(videoId => VideoPlaylist.Create(videoId, playlistId)).ToList();

            databaseContext.VideoPlaylists.AddRange(memberships);
            addedCount += memberships.Count;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        return addedCount;
    }

    public async Task<int> RemoveMembershipsAsync(string playlistId, IEnumerable<string> videoIds, CancellationToken cancellationToken)
    {
        var videoIdsToRemove = videoIds.ToHashSet(StringComparer.Ordinal);
        if (videoIdsToRemove.Count == 0)
        {
            return 0;
        }

        var membershipsToRemove = await databaseContext.VideoPlaylists
            .Where(vp => vp.PlaylistId == playlistId && videoIdsToRemove.Contains(vp.VideoId))
            .ToListAsync(cancellationToken);

        if (membershipsToRemove.Count == 0)
        {
            return 0;
        }

        databaseContext.VideoPlaylists.RemoveRange(membershipsToRemove);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return membershipsToRemove.Count;
    }

    public async Task UpdateLastMembershipSyncAtAsync(string playlistId, DateTimeOffset syncedAt, CancellationToken cancellationToken)
    {
        var playlist = await databaseContext.Playlists
            .FirstOrDefaultAsync(p => p.PlaylistId == playlistId, cancellationToken);

        if (playlist != null)
        {
            playlist.SetLastMembershipSyncAt(syncedAt);
            await databaseContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<Dictionary<string, string?>> GetPlaylistETagsAsync(IEnumerable<string> playlistIds, CancellationToken cancellationToken)
    {
        var playlistIdsList = playlistIds.ToList();
        if (playlistIdsList.Count == 0)
        {
            return new Dictionary<string, string?>();
        }

        return await databaseContext.Playlists
            .AsNoTracking()
            .Where(p => playlistIdsList.Contains(p.PlaylistId))
            .ToDictionaryAsync(p => p.PlaylistId, p => p.ETag, cancellationToken);
    }
}