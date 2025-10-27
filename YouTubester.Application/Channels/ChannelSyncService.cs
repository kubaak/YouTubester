using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using YouTubester.Application.Common;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence.Channels;
using YouTubester.Persistence.Playlists;
using YouTubester.Persistence.Videos;

namespace YouTubester.Application.Channels;

public sealed class ChannelSyncService(
    IPlaylistRepository playlistRepository,
    IYouTubeIntegration youTubeIntegration,
    IVideoRepository videoRepository,
    IChannelRepository channelRepository,
    ILogger<ChannelSyncService> logger) : IChannelSyncService
{
    private const int VideoBatchSize = 100;

    public async Task<ChannelSyncResult> SyncAsync(string channelId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting playlist sync for channel {ChannelId}", channelId);

        // 1) Validate channel exists and get uploads playlist ID
        var channel = await channelRepository.GetChannelAsync(channelId, cancellationToken);
        if (channel == null)
        {
            logger.LogError("Channel {ChannelId} not found", channelId);
            throw new InvalidOperationException($"Channel {channelId} not found");
        }

        if (string.IsNullOrWhiteSpace(channel.UploadsPlaylistId))
        {
            logger.LogError("Channel {ChannelId} has no uploads playlist ID", channelId);
            throw new InvalidOperationException($"Channel {channelId} has no uploads playlist ID");
        }

        // 2) Strategy A: Delta sync for all videos (via Uploads playlist)
        logger.LogInformation("Executing uploads delta sync for channel {ChannelId}", channelId);
        var (videosInserted, videosUpdated) = await SyncUploadsAsync(channelId, channel.UploadsPlaylistId, cancellationToken);

        // 3) Strategy B: Delta sync for playlist membership
        logger.LogInformation("Executing playlist membership sync for channel {ChannelId}", channelId);
        var (playlistsUpserted, membershipsAdded, membershipsRemoved) = await SyncPlaylistMembershipsAsync(channelId, cancellationToken);

        var result = new ChannelSyncResult(videosInserted, videosUpdated, playlistsUpserted, membershipsAdded, membershipsRemoved);

        logger.LogInformation("Playlist sync completed for channel {ChannelId}. Videos: {VideosInserted} inserted, {VideosUpdated} updated. Playlists: {PlaylistsUpserted} upserted. Memberships: {MembershipsAdded} added, {MembershipsRemoved} removed",
            channelId, result.VideosInserted, result.VideosUpdated, result.PlaylistsUpserted, result.MembershipsAdded, result.MembershipsRemoved);

        return result;
    }

    private async Task<(int VideosInserted, int VideosUpdated)> SyncUploadsAsync(string channelId, string uploadsPlaylistId, CancellationToken cancellationToken)
    {
        // Read current cutoff
        var channel = await channelRepository.GetChannelAsync(channelId, cancellationToken);
        var cutoff = channel?.LastUploadsCutoff;

        logger.LogDebug("Using uploads cutoff: {Cutoff} for channel {ChannelId}", cutoff, channelId);

        var cachedAt = DateTimeOffset.UtcNow;
        var totalVideosInserted = 0;
        var totalVideosUpdated = 0;
        var batch = new ConcurrentDictionary<string, Video>();
        var maxPublishedAt = DateTimeOffset.MinValue;
        var processedAnyVideos = false;

        var newOrUpdatedVideoIds = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var videoDto in youTubeIntegration.GetAllVideosAsync(uploadsPlaylistId, cutoff, cancellationToken))
        {
            // We already have this video (race condition)
            if (batch.ContainsKey(videoDto.VideoId))
            {
                continue;
            }

            processedAnyVideos = true;
            newOrUpdatedVideoIds.Add(videoDto.VideoId);

            // Track maximum published date for cutoff update
            if (videoDto.PublishedAt > maxPublishedAt)
            {
                maxPublishedAt = videoDto.PublishedAt;
            }

            var visibility = VideoVisibilityMapper.MapVisibility(videoDto.PrivacyStatus, videoDto.PublishedAt, DateTimeOffset.UtcNow);

            // Detect comments availability for new/updated videos
            bool? commentsAllowed = null;
            try
            {
                commentsAllowed = await youTubeIntegration.CheckCommentsAllowedAsync(videoDto.VideoId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to check comments availability for video {VideoId}", videoDto.VideoId);
                // Leave commentsAllowed as null if we can't determine it
            }

            var video = Video.Create(
                uploadsPlaylistId,
                videoDto.VideoId,
                videoDto.Title,
                videoDto.Description,
                videoDto.PublishedAt,
                videoDto.Duration,
                visibility,
                videoDto.Tags,
                videoDto.CategoryId,
                videoDto.DefaultLanguage,
                videoDto.DefaultAudioLanguage,
                videoDto.Location.HasValue
                    ? new GeoLocation(videoDto.Location.Value.lat, videoDto.Location.Value.lng)
                    : null,
                videoDto.LocationDescription,
                cachedAt,
                videoDto.ETag,
                commentsAllowed
            );

            batch.TryAdd(videoDto.VideoId, video);

            // Flush in batches to keep memory/transactions modest
            if (batch.Count >= VideoBatchSize)
            {
                var (inserted, updated) = await videoRepository.UpsertAsync(batch.Select(b => b.Value), cancellationToken);
                totalVideosInserted += inserted;
                totalVideosUpdated += updated;
                batch.Clear();
            }
        }

        // Flush remaining batch
        if (batch.Count > 0)
        {
            var (inserted, updated) = await videoRepository.UpsertAsync(batch.Select(b => b.Value), cancellationToken);
            totalVideosInserted += inserted;
            totalVideosUpdated += updated;
        }

        // Update cutoff if we processed any videos
        if (processedAnyVideos && maxPublishedAt > DateTimeOffset.MinValue)
        {
            await channelRepository.SetUploadsCutoffAsync(channelId, maxPublishedAt, cancellationToken);
            logger.LogDebug("Updated uploads cutoff to {Cutoff} for channel {ChannelId}", maxPublishedAt, channelId);
        }

        return (totalVideosInserted, totalVideosUpdated);
    }

    private async Task<(int PlaylistsUpserted, int MembershipsAdded, int MembershipsRemoved)> SyncPlaylistMembershipsAsync(string channelId, CancellationToken cancellationToken)
    {
        var currentTime = DateTimeOffset.UtcNow;
        var channel = await channelRepository.GetChannelAsync(channelId, cancellationToken);
        if (channel == null)
        {
            logger.LogError("Channel {ChannelId} not found during playlist membership sync", channelId);
            return (0, 0, 0);
        }

        var totalPlaylistsUpserted = 0;
        var totalMembershipsAdded = 0;
        var totalMembershipsRemoved = 0;

        // Fetch remote playlists with ETag support
        // For now, we don't have a stored ETag for the entire playlist collection, so we pass null
        // In the future, this could be optimized to store collection-level ETags
        var remotePlaylistDtos = await youTubeIntegration.GetMyPlaylistsAsync(null, cancellationToken);
        var remotePlaylistsList = remotePlaylistDtos
            .Where(dto => !string.IsNullOrWhiteSpace(dto.Id))
            .Select(dto => Playlist.Create(dto.Id, channelId, dto.Title, currentTime, dto.ETag))
            .ToList();

        logger.LogDebug("Found {PlaylistCount} remote playlists for channel {ChannelId}", remotePlaylistsList.Count, channelId);

        if (remotePlaylistsList.Count > 0)
        {
            // Upsert playlists into DB
            totalPlaylistsUpserted = await playlistRepository.UpsertAsync(remotePlaylistsList, cancellationToken);
            logger.LogDebug("Upserted {PlaylistsUpserted} playlists for channel {ChannelId}", totalPlaylistsUpserted, channelId);

            // For each playlist, sync memberships
            foreach (var playlist in remotePlaylistsList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                logger.LogDebug("Syncing memberships for playlist {PlaylistId} ({Title})", playlist.PlaylistId, playlist.Title);

                // Fetch remote membership video IDs
                var remoteVideoIds = new HashSet<string>(StringComparer.Ordinal);
                await foreach (var videoId in youTubeIntegration.GetPlaylistVideoIdsAsync(playlist.PlaylistId, cancellationToken))
                {
                    remoteVideoIds.Add(videoId);
                }

                // Get local membership
                var localVideoIds = await playlistRepository.GetMembershipVideoIdsAsync(playlist.PlaylistId, cancellationToken);

                // Compute differences
                var toAdd = remoteVideoIds.Except(localVideoIds).ToList();
                var toRemove = localVideoIds.Except(remoteVideoIds).ToList();

                logger.LogDebug("Playlist {PlaylistId}: {ToAddCount} to add, {ToRemoveCount} to remove",
                    playlist.PlaylistId, toAdd.Count, toRemove.Count);

                // Add memberships (repository ensures FK safety)
                if (toAdd.Count > 0)
                {
                    // First, check if we need to fetch details for unknown videos
                    var existingVideoIds = await videoRepository.GetVideoETagsAsync(toAdd, cancellationToken);
                    var unknownVideoIds = toAdd.Except(existingVideoIds.Keys).ToList();

                    if (unknownVideoIds.Count > 0)
                    {
                        logger.LogDebug("Fetching details for {UnknownVideoCount} unknown videos from playlist {PlaylistId}",
                            unknownVideoIds.Count, playlist.PlaylistId);

                        // Fetch video details in batches
                        const int batchSize = 50;
                        for (var i = 0; i < unknownVideoIds.Count; i += batchSize)
                        {
                            var batch = unknownVideoIds.Skip(i).Take(batchSize);
                            var videoDtos = await youTubeIntegration.GetVideosAsync(batch, null, cancellationToken);

                            var videosToUpsert = new List<Video>();
                            foreach (var dto in videoDtos)
                            {
                                // Detect comments availability for newly discovered videos
                                bool? commentsAllowed = null;
                                try
                                {
                                    commentsAllowed = await youTubeIntegration.CheckCommentsAllowedAsync(dto.VideoId, cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex, "Failed to check comments availability for discovered video {VideoId}", dto.VideoId);
                                }

                                var visibility = VideoVisibilityMapper.MapVisibility(dto.PrivacyStatus, dto.PublishedAt, DateTimeOffset.UtcNow);
                                var video = Video.Create(
                                    channel.UploadsPlaylistId, // We don't know the actual uploads playlist, using channel's uploads playlist
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
                                    currentTime,
                                    dto.ETag,
                                    commentsAllowed
                                );
                                videosToUpsert.Add(video);
                            }

                            if (videosToUpsert.Count > 0)
                            {
                                var (inserted, updated) = await videoRepository.UpsertAsync(videosToUpsert, cancellationToken);
                                // Note: We don't track these counts separately since they're for playlist membership videos
                            }
                        }
                    }

                    var addedCount = await playlistRepository.AddMembershipsAsync(playlist.PlaylistId, toAdd, cancellationToken);
                    totalMembershipsAdded += addedCount;
                }

                // Remove memberships
                if (toRemove.Count > 0)
                {
                    var removedCount = await playlistRepository.RemoveMembershipsAsync(playlist.PlaylistId, toRemove, cancellationToken);
                    totalMembershipsRemoved += removedCount;
                }

                // Update last membership sync timestamp
                await playlistRepository.UpdateLastMembershipSyncAtAsync(playlist.PlaylistId, currentTime, cancellationToken);
            }
        }

        return (totalPlaylistsUpserted, totalMembershipsAdded, totalMembershipsRemoved);
    }
}