using YouTubester.Integration.Dtos;

namespace YouTubester.Integration;

public interface IYouTubeIntegration
{
    IAsyncEnumerable<VideoDto> GetAllVideosAsync(
        string uploadsPlaylistId, DateTimeOffset? publishedAfter, CancellationToken cancellationToken);

    IAsyncEnumerable<CommentThreadDto> GetUnansweredTopLevelCommentsAsync(
        string channelId, string videoId, CancellationToken cancellationToken);

    Task ReplyAsync(string parentCommentId, string text, CancellationToken cancellationToken);

    Task<VideoDetailsDto?> GetVideoDetailsAsync(string videoId, CancellationToken cancellationToken);

    Task UpdateVideoAsync(string videoId, string title, string description, IReadOnlyList<string> tags,
        string? categoryId, string? defaultLanguage, string? defaultAudioLanguage,
        (double lat, double lng)? location, string? locationDescription, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetPlaylistsContainingAsync(string videoId, CancellationToken cancellationToken);
    Task AddVideoToPlaylistAsync(string playlistId, string videoId, CancellationToken cancellationToken);

    IAsyncEnumerable<PlaylistDto> GetPlaylistsAsync(string channelId,
        CancellationToken cancellationToken);

    IAsyncEnumerable<string> GetPlaylistVideoIdsAsync(string playlistId, CancellationToken cancellationToken);

    Task<bool?> CheckCommentsAllowedAsync(string videoId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VideoDto>> GetVideosAsync(IEnumerable<string> videoIds,
        CancellationToken cancellationToken);
}