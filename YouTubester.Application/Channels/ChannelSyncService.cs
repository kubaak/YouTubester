using Microsoft.Extensions.Logging;
using YouTubester.Abstractions.Channels;
using YouTubester.Abstractions.Playlists;
using YouTubester.Abstractions.Videos;
using YouTubester.Application.Common;
using YouTubester.Application.Contracts.Channels;
using YouTubester.Domain;
using YouTubester.Integration;

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
    /// Pulls channel metadata from YouTube and persists a canonical Channel aggregate for the given user.
    /// - Creates a new row if it does not exist.
    /// - Otherwise applies a remote snapshot (Name, UploadsPlaylistId, ETag) and updates only if changed.
    /// Returns the up-to-date aggregate.
    /// </summary>
    public async Task<Channel> PullChannelAsync(string userId, string channelName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(channelName))
        {
            throw new ArgumentException("Channel name is required.", nameof(channelName));
        }

        // Pull canonical channel details (ChannelId, Title, UploadsPlaylistId, ETag)
        var channelDto = await youTubeIntegration.GetChannelAsync(userId, channelName, cancellationToken) ??
                         throw new NotFoundException($"Channel '{channelName}' not found on YouTube.");

        var now = DateTimeOffset.UtcNow;

        // Prefer lookup by canonical ChannelId
        var existingChannel = await channelRepository.GetChannelAsync(channelDto.Id, cancellationToken);

        if (existingChannel is null)
        {
            // New aggregate
            var channel = Channel.Create(
                channelDto.Id,
                userId,
                channelDto.Name,
                channelDto.UploadsPlaylistId,
                now,
                null,
                channelDto.ETag
            );

            await channelRepository.UpsertChannelAsync(channel, cancellationToken);
            return channel;
        }

        // Apply remote snapshot via domain behavior; persist only if dirty.
        var dirty = existingChannel.ApplyRemoteSnapshot(
            channelDto.Name,
            channelDto.UploadsPlaylistId,
            channelDto.ETag,
            now
        );

        if (dirty)
        {
            await channelRepository.UpsertChannelAsync(existingChannel, cancellationToken);
        }

        return existingChannel;
    }

    public async Task<IReadOnlyList<AvailableChannelDto>> GetAvailableYoutubeChannelsForUserAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var youTubeChannels = await youTubeIntegration.GetUserChannelsAsync(userId, cancellationToken);
        if (youTubeChannels.Count == 0)
        {
            return Array.Empty<AvailableChannelDto>();
        }

        var availableChannels = new List<AvailableChannelDto>(youTubeChannels.Count);
        foreach (var youTubeChannel in youTubeChannels)
        {
            availableChannels.Add(new AvailableChannelDto(
                youTubeChannel.Id,
                youTubeChannel.Name,
                youTubeChannel.UploadsPlaylistId,
                youTubeChannel.ETag));
        }

        return availableChannels;
    }

    public async Task SyncChannelsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var channels = await channelRepository.GetChannelsForUserAsync(userId, cancellationToken);
        if (channels.Count == 0)
        {
            logger.LogInformation("No channels found for user {UserId}; nothing to sync", userId);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var channel in channels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SyncInternalAsync(userId, channel, now, cancellationToken);
        }
    }

    private async Task<ChannelSyncResult> SyncInternalAsync(
        string userId,
        Channel channel,
        DateTimeOffset now,
        CancellationToken ct)
    {
        logger.LogInformation("Starting playlist sync for channel {ChannelId}", channel.ChannelId);

        var (videosInserted, videosUpdated) = await SyncUploadsAsync(userId, channel, now, ct);

        var (playlistsInserted, playlistsUpdated, membershipsAdded, membershipsRemoved) =
            await SyncPlaylistMembershipsAsync(userId, channel, now, ct);

        var result = new ChannelSyncResult(videosInserted, videosUpdated, playlistsInserted,
            playlistsUpdated, membershipsAdded, membershipsRemoved);

        logger.LogInformation(
            "Playlist sync completed for channel {ChannelId}. Videos: {Ins} inserted, Videos: {Upd} updated." +
            "Playlists: {PlIns} inserted, Playlists: {PlUp} updated. Memberships: {Add} added, {Rem} removed",
            channel.ChannelId, result.VideosInserted, result.VideosUpdated, result.PlaylistsInserted,
            result.PlaylistsUpdated, result.MembershipsAdded, result.MembershipsRemoved);

        return result;
    }

    private async Task<(int videosInserted, int videosUpdated)> SyncUploadsAsync(
        string userId,
        Channel channel,
        DateTimeOffset now,
        CancellationToken cancellationToken)
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

        await foreach (var videoDto in youTubeIntegration.GetAllVideosAsync(userId, uploadsPlaylistId, cutoff,
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
        SyncPlaylistMembershipsAsync(
            string userId,
            Channel channel,
            DateTimeOffset now,
            CancellationToken cancellationToken)
    {
        var channelId = channel.ChannelId;
        var totalMembershipsAdded = 0;
        var totalMembershipsRemoved = 0;

        var playlistDtos = youTubeIntegration.GetPlaylistsAsync(userId, channelId, cancellationToken);
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
            await foreach (var videoId in youTubeIntegration.GetPlaylistVideoIdsAsync(userId, playlist.PlaylistId,
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
                // Only add memberships for videos that are already known uploads for this channel.
                // This prevents importing videos that belong to other channels but are present in the user's playlists.
                var existing = await videoRepository.GetVideoETagsAsync(toAdd, cancellationToken);
                var knownVideoIds = toAdd.Where(existing.ContainsKey).ToHashSet(StringComparer.Ordinal);

                if (knownVideoIds.Count > 0)
                {
                    totalMembershipsAdded +=
                        await playlistRepository.AddMembershipsAsync(playlist.PlaylistId, knownVideoIds,
                            cancellationToken);
                }
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