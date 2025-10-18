using YouTubester.Application.Contracts.Videos;

namespace YouTubester.Application;

public interface IVideoService
{
    Task<SyncVideosResult> SyncChannelVideosAsync(string uploadPlaylistId, CancellationToken ct);
}