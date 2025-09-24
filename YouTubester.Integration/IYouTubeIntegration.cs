using YouTubester.Integration.Dtos;

namespace YouTubester.Integration;

public interface IYouTubeIntegration
{
    Task<string> GetMyChannelIdAsync(CancellationToken ct = default);
    
    IAsyncEnumerable<string> GetAllPublicVideoIdsAsync(CancellationToken ct = default);
    Task<VideoDto?> GetVideoAsync(string videoId, CancellationToken ct = default);
    
    IAsyncEnumerable<CommentThreadDto> GetUnansweredTopLevelCommentsAsync(
        string videoId,
        CancellationToken ct = default);
    
    Task ReplyAsync(string parentCommentId, string text, CancellationToken ct = default);
}