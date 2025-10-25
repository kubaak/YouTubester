using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YouTubester.Application.Common;
using YouTubester.Application.Contracts;
using YouTubester.Application.Contracts.Videos;
using YouTubester.Application.Exceptions;
using YouTubester.Application.Options;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence.Videos;

namespace YouTubester.Application;

public class VideoService(
    IVideoRepository repo,
    IYouTubeIntegration yt,
    IOptions<VideoListingOptions> options,
    ILogger<VideoService> logger) : IVideoService
{
    private const int BatchCapacity = 100;

    public async Task<SyncVideosResult> SyncChannelVideosAsync(string uploadPlaylistId, CancellationToken ct)
    {
        var cachedAt = DateTimeOffset.UtcNow;
        var total = 0;
        var batch = new ConcurrentDictionary<string, Video>();

        await foreach (var videoDto in yt.GetAllVideosAsync(uploadPlaylistId, null, ct))
        {
            //We already have this video (race condition)
            if (batch.ContainsKey(videoDto.VideoId))
            {
                continue;
            }

            total++;

            batch.TryAdd(videoDto.VideoId, Video.Create(
                uploadPlaylistId,
                videoDto.VideoId,
                videoDto.Title,
                videoDto.Description,
                videoDto.PublishedAt,
                videoDto.Duration,
                VideoVisibilityMapper.MapVisibility(videoDto.PrivacyStatus, videoDto.PublishedAt, DateTimeOffset.UtcNow),
                videoDto.Tags,
                videoDto.CategoryId,
                videoDto.DefaultLanguage, videoDto.DefaultAudioLanguage,
                videoDto.Location.HasValue
                    ? new GeoLocation(videoDto.Location.Value.lat, videoDto.Location.Value.lng)
                    : null,
                videoDto.LocationDescription,
                cachedAt
            ));

            // flush in batches to keep memory/transactions modest
            if (batch.Count >= BatchCapacity)
            {
                await repo.UpsertAsync(batch.Select(b => b.Value), ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await repo.UpsertAsync(batch.Select(b => b.Value), ct);
        }

        // For a more detailed result, have repo return (inserted, updated). Here we just report total touched.
        return new SyncVideosResult(0, 0, total);
    }

    public async Task<PagedResult<VideoListItemDto>> GetVideosAsync(string? title, VideoVisibility[]? visibility,
        int? pageSize, string? pageToken, CancellationToken ct)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        if (string.IsNullOrEmpty(normalizedTitle))
        {
            normalizedTitle = null;
        }

        var opts = options.Value;
        var effectivePageSize = pageSize ?? opts.DefaultPageSize;
        if (effectivePageSize < 1 || effectivePageSize > opts.MaxPageSize)
        {
            throw new InvalidPageSizeException(effectivePageSize, opts.MaxPageSize);
        }

        var visibilityBinding = visibility is null ? string.Empty : string.Join(',', visibility.OrderBy(v => v));
        var binding = $"{normalizedTitle ?? string.Empty}|{visibilityBinding}";

        DateTimeOffset? afterPublishedAtUtc = null;
        string? afterVideoId = null;
        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            if (!VideosPageToken.TryParse(pageToken, out var publishedAt, out var videoId, out var tokenBinding))
            {
                logger.LogWarning("Invalid page token received");
                throw new InvalidPageTokenException();
            }

            if (!string.IsNullOrEmpty(tokenBinding) && !string.Equals(tokenBinding, binding, StringComparison.Ordinal))
            {
                logger.LogWarning("Page token binding mismatch");
                throw new InvalidPageTokenException("Page token does not match current filters.");
            }

            afterPublishedAtUtc = publishedAt;
            afterVideoId = videoId;
        }

        // Fetch one extra item to determine if there's a next page
        var take = effectivePageSize + 1;
        var videos =
            await repo.GetVideosPageAsync(normalizedTitle, visibility, afterPublishedAtUtc, afterVideoId, take, ct);

        // Determine if there are more items
        var hasMore = videos.Count > effectivePageSize;
        var itemsToReturn = hasMore ? videos.Take(effectivePageSize).ToList() : videos;

        string? nextPageToken = null;
        if (hasMore && itemsToReturn.Count > 0)
        {
            var lastItem = itemsToReturn[^1];
            nextPageToken = VideosPageToken.Serialize(lastItem.PublishedAt, lastItem.VideoId, binding);
        }

        var items = itemsToReturn.Select(v => new VideoListItemDto
        {
            VideoId = v.VideoId,
            Title = v.Title,
            PublishedAt = v.PublishedAt,
            ThumbnailUrl = v.ThumbnailUrl
        }).ToList();

        return new PagedResult<VideoListItemDto> { Items = items, NextPageToken = nextPageToken };
    }

}