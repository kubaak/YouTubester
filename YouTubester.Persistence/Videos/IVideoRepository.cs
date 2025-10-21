using YouTubester.Domain;

namespace YouTubester.Persistence.Videos;

public interface IVideoRepository
{
    Task<List<Video>> GetAllVideosAsync(CancellationToken cancellationToken);
    Task<int> UpsertAsync(IEnumerable<Video> videos, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a page of videos with optional title filtering and cursor-based pagination.
    /// </summary>
    /// <param name="title">Optional title filter (case-insensitive substring match).</param>
    /// <param name="visibilities">Optional set of visibilities to include.</param>
    /// <param name="afterPublishedAtUtc">Cursor: published date to search after (exclusive).</param>
    /// <param name="afterVideoId">Cursor: video ID to search after when published dates are equal.</param>
    /// <param name="take">Maximum number of items to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of videos ordered by PublishedAt DESC, VideoId DESC.</returns>
    Task<List<Video>> GetVideosPageAsync(string? title, IReadOnlyCollection<VideoVisibility>? visibilities, DateTimeOffset? afterPublishedAtUtc, string? afterVideoId, int take, CancellationToken ct);
}
