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

        await foreach (var videoDto in youTubeIntegration.GetAllVideosAsync(uploadsPlaylistId, cutoff, cancellationToken))
        {
            // We already have this video (race condition)
            if (batch.ContainsKey(videoDto.VideoId))
            {
                continue;
            }

            processedAnyVideos = true;

            // Track maximum published date for cutoff update
            if (videoDto.PublishedAt > maxPublishedAt)
            {
                maxPublishedAt = videoDto.PublishedAt;
            }

            var visibility = VideoVisibilityMapper.MapVisibility(videoDto.PrivacyStatus, videoDto.PublishedAt, DateTimeOffset.UtcNow);
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
                cachedAt
            );

            batch.TryAdd(videoDto.VideoId, video);

            // Flush in batches to keep memory/transactions modest
            if (batch.Count >= VideoBatchSize)
            {
                var changedCount = await videoRepository.UpsertAsync(batch.Select(b => b.Value), cancellationToken);
                totalVideosInserted += changedCount; // Repository returns total changed, approximation for now
                batch.Clear();
            }
        }

        // Flush remaining batch
        if (batch.Count > 0)
        {
            var changedCount = await videoRepository.UpsertAsync(batch.Select(b => b.Value), cancellationToken);
            totalVideosUpdated += changedCount;
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
        var totalPlaylistsUpserted = 0;
        var totalMembershipsAdded = 0;
        var totalMembershipsRemoved = 0;

        // Fetch remote playlists
        var remotePlaylistsList = new List<Playlist>();
        await foreach (var (playlistId, title) in youTubeIntegration.GetPlaylistsAsync(channelId, cancellationToken))
        {
            var playlist = Playlist.Create(playlistId, channelId, title, currentTime);
            remotePlaylistsList.Add(playlist);
        }

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