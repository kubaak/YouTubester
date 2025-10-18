using YouTubester.Domain;

namespace YouTubester.Persistence.Videos;

public interface IVideoRepository
{
    Task<int> UpsertAsync(IEnumerable<Video> videos, CancellationToken cancellationToken = default);
}