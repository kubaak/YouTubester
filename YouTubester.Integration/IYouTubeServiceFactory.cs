using Google.Apis.YouTube.v3;

namespace YouTubester.Integration;

public interface IYouTubeServiceFactory
{
    Task<YouTubeService> CreateAsync(string userId, CancellationToken cancellationToken);
}