using YouTubester.Application.Contracts;
using YouTubester.Application.Contracts.Videos;

namespace YouTubester.Application;

public interface IVideoService
{
    Task<SyncVideosResult> SyncChannelVideosAsync(string uploadPlaylistId, CancellationToken ct);
    
    /// <summary>
    /// Gets a paginated list of videos with optional title filtering.
    /// </summary>
    /// <param name="title">Optional case-insensitive substring filter for video titles.</param>
    /// <param name="visibility">Optional comma-separated list of visibilities: public, unlisted, private, scheduled.</param>
    /// <param name="pageSize">Number of items per page (1-100, defaults to configured DefaultPageSize).</param>
    /// <param name="pageToken">Cursor token for pagination, or null for first page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated result containing videos and optional next page token.</returns>
    /// <exception cref="Exceptions.InvalidPageSizeException">When pageSize is outside valid range.</exception>
    /// <exception cref="Exceptions.InvalidPageTokenException">When pageToken is malformed.</exception>
    /// <exception cref="Exceptions.InvalidVisibilityException">When visibility contains invalid values.</exception>
    Task<PagedResult<VideoListItemDto>> GetVideosAsync(string? title, string? visibility, int? pageSize, string? pageToken, CancellationToken ct);
}
