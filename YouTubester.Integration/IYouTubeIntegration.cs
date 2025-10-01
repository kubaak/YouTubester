using YouTubester.Integration.Dtos;

namespace YouTubester.Integration;

public interface IYouTubeIntegration
{
    Task<string> GetMyChannelIdAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<string> GetAllPublicVideoIdsAsync(CancellationToken cancellationToken);
    Task<VideoDto?> GetVideoAsync(string videoId, CancellationToken cancellationToken);
    IAsyncEnumerable<CommentThreadDto> GetUnansweredTopLevelCommentsAsync(
        string videoId, CancellationToken cancellationToken);
    Task ReplyAsync(string parentCommentId, string text, CancellationToken cancellationToken);
    
    Task<VideoDetailsDto?> GetVideoDetailsAsync(string videoId, CancellationToken cancellationToken);
    Task UpdateVideoAsync(string videoId, string title, string description, IReadOnlyList<string> tags,
        string? categoryId, string? defaultLanguage, string? defaultAudioLanguage, 
        (double lat, double lng)? location, string? locationDescription, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetPlaylistsContainingAsync(string videoId, CancellationToken cancellationToken);
    Task AddVideoToPlaylistAsync(string playlistId, string videoId, CancellationToken cancellationToken);
}