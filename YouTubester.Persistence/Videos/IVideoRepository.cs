using YouTubester.Domain;

namespace YouTubester.Persistence.Videos;

public interface IVideoRepository
{
    Task<List<Video>> GetAllVideosAsync(CancellationToken cancellationToken);
    Task<int> UpsertAsync(IEnumerable<Video> videos, CancellationToken cancellationToken = default);
}