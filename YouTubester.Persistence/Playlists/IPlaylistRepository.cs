using YouTubester.Domain;

namespace YouTubester.Persistence.Playlists;

public interface IPlaylistRepository
{
    Task<List<Playlist>> GetByChannelAsync(string channelId, CancellationToken cancellationToken);
    Task<Playlist?> GetAsync(string playlistId, CancellationToken cancellationToken);
    Task<int> UpsertAsync(IEnumerable<Playlist> playlists, CancellationToken cancellationToken);
    Task<HashSet<string>> GetMembershipVideoIdsAsync(string playlistId, CancellationToken cancellationToken);
    Task<int> AddMembershipsAsync(string playlistId, IEnumerable<string> videoIds, CancellationToken cancellationToken);
    Task<int> RemoveMembershipsAsync(string playlistId, IEnumerable<string> videoIds, CancellationToken cancellationToken);
    Task UpdateLastMembershipSyncAtAsync(string playlistId, DateTimeOffset syncedAt, CancellationToken cancellationToken);
}