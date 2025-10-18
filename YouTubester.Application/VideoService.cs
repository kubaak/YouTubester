using System.Collections.Concurrent;
using YouTubester.Application.Contracts.Videos;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence.Videos;

namespace YouTubester.Application;

public class VideoService(IVideoRepository repo, IYouTubeIntegration yt): IVideoService
{
    private const int BatchCapacity = 100;
    public async Task<SyncVideosResult> SyncChannelVideosAsync(CancellationToken ct)
    {
        var cachedAt = DateTimeOffset.UtcNow;
        var total = 0;
        var batch = new ConcurrentDictionary<string,Video>();
        
        await foreach (var videoDto in yt.GetAllVideosAsync(null, ct))
        {
            //We already have this video (race condition)
            if (batch.ContainsKey(videoDto.VideoId))
            {
                continue;
            }
            total++;
            
            batch.TryAdd(videoDto.VideoId, Video.Create(
                videoDto.ChannelId,
                videoDto.VideoId, 
                videoDto.Title,
                videoDto.Description,
                videoDto.PublishedAt,
                videoDto.Duration,
                MapVisibility(videoDto.PrivacyStatus,videoDto.PublishedAt, DateTimeOffset.UtcNow),
                videoDto.Tags,
                videoDto.CategoryId,
                videoDto.DefaultLanguage, videoDto.DefaultAudioLanguage,
                videoDto.Location.HasValue ? new GeoLocation(videoDto.Location.Value.lat, videoDto.Location.Value.lng) : null,
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
            await repo.UpsertAsync(batch.Select(b => b.Value), ct);

        // For a more detailed result, have repo return (inserted, updated). Here we just report total touched.
        return new SyncVideosResult(Inserted: 0, Updated: 0, Total: total);
    }
    
    static VideoVisibility MapVisibility(string? privacyStatus, DateTimeOffset? publishAtUtc, DateTimeOffset nowUtc)
    {
        if (string.Equals(privacyStatus, "private", StringComparison.OrdinalIgnoreCase)
            && publishAtUtc.HasValue
            && publishAtUtc.Value > nowUtc)
            return VideoVisibility.Scheduled;

        return privacyStatus?.ToLowerInvariant() switch
        {
            "public"   => VideoVisibility.Public,
            "unlisted" => VideoVisibility.Unlisted,
            "private"  => VideoVisibility.Private,
            _          => VideoVisibility.Private
        };
    }
}