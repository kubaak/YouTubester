using YouTubester.Abstractions.Channels;
using YouTubester.Integration.Dtos;

namespace YouTubester.Integration;

public interface IYouTubeIntegration
{
    Task<ChannelDto?> GetChannelAsync(string userId, string channelId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChannelDto>> GetUserChannelsAsync(string userId, CancellationToken cancellationToken);

    Task<UserChannelDto?> GetCurrentChannelAsync(string accessToken, CancellationToken cancellationToken);

    IAsyncEnumerable<VideoDto> GetAllVideosAsync(
        string userId,
        string uploadsPlaylistId,
        DateTimeOffset? publishedAfter,
        CancellationToken cancellationToken);

    IAsyncEnumerable<CommentThreadDto> GetUnansweredTopLevelCommentsAsync(
        string userId,
        string channelId,
        string videoId,
        CancellationToken cancellationToken);

    Task ReplyAsync(string userId, string parentCommentId, string text, CancellationToken cancellationToken);

    Task UpdateVideoAsync(
        string userId,
        string videoId,
        string title,
        string description,
        IReadOnlyList<string> tags,
        string? categoryId,
        string? defaultLanguage,
        string? defaultAudioLanguage,
        (double lat, double lng)? location,
        string? locationDescription,
        CancellationToken cancellationToken);

    Task AddVideoToPlaylistAsync(string userId, string playlistId, string videoId, CancellationToken cancellationToken);

    IAsyncEnumerable<PlaylistDto> GetPlaylistsAsync(
        string userId,
        string channelId,
        CancellationToken cancellationToken);

    IAsyncEnumerable<string> GetPlaylistVideoIdsAsync(
        string userId,
        string playlistId,
        CancellationToken cancellationToken);

    Task<bool?> CheckCommentsAllowedAsync(string userId, string videoId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VideoDto>> GetVideosAsync(
        string userId,
        IEnumerable<string> videoIds,
        CancellationToken cancellationToken);
}
