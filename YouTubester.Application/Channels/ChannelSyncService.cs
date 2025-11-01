using Microsoft.Extensions.Logging;
using YouTubester.Application.Common;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence.Channels;
using YouTubester.Persistence.Playlists;
using YouTubester.Persistence.Videos;

namespace YouTubester.Application.Channels;

//todos
//1) Short-circuit unchanged playlists via stored ETags
//2) Use a transactions
public sealed class ChannelSyncService(
    IPlaylistRepository playlistRepository,
    IYouTubeIntegration youTubeIntegration,
    IVideoRepository videoRepository,
    IChannelRepository channelRepository,
    ILogger<ChannelSyncService> logger) : IChannelSyncService
{
    private const int VideoBatchSize = 100;

    /// <summary>
    /// Pulls channel metadata from YouTube and persists a canonical Channel aggregate.
    /// - Creates a new row if it doesn't exist.
    /// - Otherwise applies a remote snapshot (Name, UploadsPlaylistId, ETag) and updates only if changed.
    /// Returns the up-to-date aggregate.
    /// </summary>
    public async Task<Channel> PullChannelAsync(string channelName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            throw new ArgumentException("Channel name is required.", nameof(channelName));
        }

        // Pull canonical channel details (ChannelId, Title, UploadsPlaylistId, ETag)
        var dto = await youTubeIntegration.GetChannelAsync(channelName);
        if (dto is null)
        {
            throw new NotFoundException($"Channel '{channelName}' not found on YouTube.");
        }

        var now = DateTimeOffset.UtcNow;

        // Prefer lookup by canonical ChannelId
        var existing = await channelRepository.GetChannelAsync(dto.Id, ct);

        if (existing is null)
        {
            // New aggregate
            var channel = Channel.Create(
                dto.Id,
                dto.Name,
                dto.UploadsPlaylistId,
                now,
                null,
                dto.ETag
            );

            await channelRepository.UpsertChannelAsync(channel, ct);
            return channel;
        }

        // Apply remote snapshot via domain behavior; persist only if dirty.
        var dirty = existing.ApplyRemoteSnapshot(
            dto.Name,
            dto.UploadsPlaylistId,
            dto.ETag,
            now
        );

        if (dirty)
        {
            await channelRepository.UpsertChannelAsync(existing, ct);
        }

        return existing;
    }

    public async Task<ChannelSyncResult> SyncByNameAsync(string channelName, CancellationToken ct)
    {
        var channel = await channelRepository.GetChannelByNameAsync(channelName, ct) ??
                      throw new NotFoundException($"Channel '{channelName}' not found.");

        var now = DateTimeOffset.UtcNow;
        return await SyncInternalAsync(channel, now, ct);
    }

    private async Task<ChannelSyncResult> SyncInternalAsync(Channel channel, DateTimeOffset now, CancellationToken ct)
    {
        logger.LogInformation("Starting playlist sync for channel {ChannelId}", channel.ChannelId);

        var (videosInserted, videosUpdated) = await SyncUploadsAsync(channel, now, ct);

        var (playlistsInserted, playlistsUpdated, membershipsAdded, membershipsRemoved) =
            await SyncPlaylistMembershipsAsync(channel, now, ct);

        var result = new ChannelSyncResult(videosInserted, videosUpdated, playlistsInserted,
            playlistsUpdated, membershipsAdded, membershipsRemoved);

        logger.LogInformation(
            "Playlist sync completed for channel {ChannelId}. Videos: {Ins} inserted, Videos: {Upd} updated." +
            "Playlists: {PlIns} inserted, Playlists: {PlUp} updated. Memberships: {Add} added, {Rem} removed",
            channel.ChannelId, result.VideosInserted, result.VideosUpdated, result.PlaylistsInserted,
            result.PlaylistsUpdated, result.MembershipsAdded, result.MembershipsRemoved);

        return result;
    }

    private async Task<(int videosInserted, int videosUpdated)> SyncUploadsAsync(Channel channel,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var channelId = channel.ChannelId;
        var uploadsPlaylistId = channel.UploadsPlaylistId;
        var cutoff = channel.LastUploadsCutoff;

        logger.LogInformation("Executing uploads delta sync for channel {ChannelId} (cutoff: {Cutoff})", channelId,
            cutoff);

        var totalVideosInserted = 0;
        var totalVideosUpdated = 0;
        var batch = new List<Video>(VideoBatchSize);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var maxPublishedAt = cutoff ?? DateTimeOffset.MinValue;
        var processedAny = false;

        await foreach (var videoDto in youTubeIntegration.GetAllVideosAsync(uploadsPlaylistId, cutoff,
                           cancellationToken))
        {
            if (!seen.Add(videoDto.VideoId))
            {
                continue;
            }

            processedAny = true;
            if (videoDto.PublishedAt > maxPublishedAt)
            {
                maxPublishedAt = videoDto.PublishedAt;
            }

            var visibility =
                VideoVisibilityMapper.MapVisibility(videoDto.PrivacyStatus, videoDto.PublishedAt,
                    now);

            batch.Add(Video.Create(
                uploadsPlaylistId, videoDto.VideoId, videoDto.Title, videoDto.Description,
                videoDto.PublishedAt, videoDto.Duration, visibility, videoDto.Tags,
                videoDto.CategoryId, videoDto.DefaultLanguage, videoDto.DefaultAudioLanguage, videoDto.Location.HasValue
                    ? new GeoLocation(videoDto.Location.Value.lat, videoDto.Location.Value.lng)
                    : null,
                videoDto.LocationDescription, now, videoDto.ETag
            ));

            if (batch.Count < VideoBatchSize)
            {
                continue;
            }

            var (inserted, changed) = await videoRepository.UpsertAsync(batch, cancellationToken);
            totalVideosUpdated += changed;
            totalVideosInserted += inserted;
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            var (inserted, changed) = await videoRepository.UpsertAsync(batch, cancellationToken);
            totalVideosUpdated += changed;
            totalVideosInserted += inserted;
        }

        if (!processedAny || (cutoff.HasValue && maxPublishedAt <= cutoff.Value))
        {
            return (totalVideosInserted, totalVideosUpdated);
        }

        await channelRepository.SetUploadsCutoffAsync(channelId, maxPublishedAt, cancellationToken);
        logger.LogDebug("Updated uploads cutoff to {Cutoff} for channel {ChannelId}", maxPublishedAt, channelId);

        return (totalVideosInserted, totalVideosUpdated);
    }

    private async Task<(int PlaylistsInserted, int PlaylistsUpdated, int MembershipsAdded, int MembershipsRemoved)>
        SyncPlaylistMembershipsAsync(Channel channel, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var channelId = channel.ChannelId;
        var totalMembershipsAdded = 0;
        var totalMembershipsRemoved = 0;

        var playlistDtos = youTubeIntegration.GetPlaylistsAsync(channelId, cancellationToken);
        var remotePlaylists = new List<Playlist>();
        await foreach (var dto in playlistDtos)
        {
            if (!string.IsNullOrWhiteSpace(dto.Id))
            {
                remotePlaylists.Add(Playlist.Create(dto.Id, channelId, dto.Title, now, dto.ETag));
            }
        }

        if (remotePlaylists.Count == 0)
        {
            return (0, 0, 0, 0);
        }

        var (totalPlaylistsInserted, totalPlaylistsUpdated) =
            await playlistRepository.UpsertAsync(remotePlaylists, cancellationToken);

        foreach (var playlist in remotePlaylists)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remoteVideoIds = new HashSet<string>(StringComparer.Ordinal);
            await foreach (var videoId in youTubeIntegration.GetPlaylistVideoIdsAsync(playlist.PlaylistId,
                               cancellationToken))
            {
                remoteVideoIds.Add(videoId);
            }

            var localVideoIds =
                await playlistRepository.GetMembershipVideoIdsAsync(playlist.PlaylistId, cancellationToken);

            var toAdd = remoteVideoIds.Except(localVideoIds).ToHashSet();
            var toRemove = localVideoIds.Except(remoteVideoIds).ToList();

            if (toAdd.Count > 0)
            {
                var existing = await videoRepository.GetVideoETagsAsync(toAdd, cancellationToken);
                //the videos that were not imported in SyncUploadsAsync
                var unknown = toAdd.Where(id => !existing.ContainsKey(id)).ToList();

                if (unknown.Count > 0)
                {
                    const int batchSize = 50;
                    for (var i = 0; i < unknown.Count; i += batchSize)
                    {
                        var slice = unknown.Skip(i).Take(batchSize);
                        var dtos = await youTubeIntegration.GetVideosAsync(slice, cancellationToken);

                        var upserts = new List<Video>();
                        foreach (var dto in dtos)
                        {
                            var visibility = VideoVisibilityMapper.MapVisibility(dto.PrivacyStatus, dto.PublishedAt,
                                now);
                            upserts.Add(Video.Create(
                                channel.UploadsPlaylistId,
                                dto.VideoId,
                                dto.Title,
                                dto.Description,
                                dto.PublishedAt,
                                dto.Duration,
                                visibility,
                                dto.Tags,
                                dto.CategoryId,
                                dto.DefaultLanguage,
                                dto.DefaultAudioLanguage,
                                dto.Location.HasValue
                                    ? new GeoLocation(dto.Location.Value.lat, dto.Location.Value.lng)
                                    : null,
                                dto.LocationDescription,
                                now,
                                dto.ETag
                            ));
                        }

                        if (upserts.Count > 0)
                        {
                            await videoRepository.UpsertAsync(upserts, cancellationToken);
                        }
                    }
                }

                totalMembershipsAdded +=
                    await playlistRepository.AddMembershipsAsync(playlist.PlaylistId, toAdd, cancellationToken);
            }

            if (toRemove.Count > 0)
            {
                totalMembershipsRemoved +=
                    await playlistRepository.RemoveMembershipsAsync(playlist.PlaylistId, toRemove, cancellationToken);
            }

            await playlistRepository.UpdateLastMembershipSyncAtAsync(playlist.PlaylistId, now, cancellationToken);
        }

        return (totalPlaylistsInserted, totalPlaylistsUpdated, totalMembershipsAdded, totalMembershipsRemoved);
    }
}