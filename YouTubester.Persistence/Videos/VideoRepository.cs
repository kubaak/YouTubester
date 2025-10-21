using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;

namespace YouTubester.Persistence.Videos;

public sealed class VideoRepository(YouTubesterDb db) : IVideoRepository
{
    public async Task<List<Video>> GetAllVideosAsync(CancellationToken cancellationToken)
    {
        return await db.Videos
            .AsNoTracking()
            .OrderByDescending(v => v.PublishedAt)
            .ThenByDescending(v => v.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> UpsertAsync(IEnumerable<Video> videos, CancellationToken cancellationToken)
    {
        var list = videos.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        var ids = list.Select(i => i.VideoId).ToHashSet();

        var existing = await db.Videos.Where(v => ids.Contains(v.VideoId))
            .ToDictionaryAsync(v => v.VideoId, v => v, cancellationToken);

        var now = DateTimeOffset.UtcNow; //todo provider
        var inserts = 0;
        var updates = 0;

        foreach (var video in list)
        {
            if (!existing.TryGetValue(video.VideoId, out var row))
            {
                db.Add(video);
                inserts++;
            }
            else
            {
                var changed = row.ApplyDetails(
                    video.Title, video.Description, video.PublishedAt, video.Duration,
                    video.Visibility, video.Tags, video.CategoryId, video.DefaultLanguage,
                    video.DefaultAudioLanguage, video.Location, video.LocationDescription, now,
                    video.ThumbnailUrl
                );
                if (changed)
                {
                    updates++;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return inserts + updates;
    }

    public async Task<List<Video>> GetVideosPageAsync(string? title, IReadOnlyCollection<VideoVisibility>? visibilities, DateTimeOffset? afterPublishedAtUtc, string? afterVideoId, int take, CancellationToken ct)
    {
        var query = db.Videos.AsNoTracking();

        // Apply title filter (case-insensitive substring match)
        if (!string.IsNullOrWhiteSpace(title))
        {
            var trimmedTitle = title.Trim();
            var pattern = $"%{trimmedTitle}%";
            query = query.Where(v => EF.Functions.Like(
                EF.Functions.Collate(v.Title ?? "", "NOCASE"), pattern));
        }

        // Apply visibility filter
        if (visibilities is { Count: > 0 })
        {
            query = query.Where(v => visibilities.Contains(v.Visibility));
        }

        // Apply cursor filter for "strictly earlier" items in descending order
        if (afterPublishedAtUtc.HasValue && !string.IsNullOrEmpty(afterVideoId))
        {
            query = query.Where(v => 
                v.PublishedAt < afterPublishedAtUtc.Value ||
                (v.PublishedAt == afterPublishedAtUtc.Value && 
                 string.Compare(v.VideoId, afterVideoId, StringComparison.Ordinal) < 0));
        }

        return await query
            .OrderByDescending(v => v.PublishedAt)
            .ThenByDescending(v => v.VideoId)
            .Take(take)
            .ToListAsync(ct);
    }
}
