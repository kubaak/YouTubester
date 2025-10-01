using Google.Apis.YouTube.v3;

namespace YouTubester.Integration;

public interface IYouTubeClientFactory
{
    Task<YouTubeService> CreateAsync(CancellationToken cancellationToken);
}