# PlaylistSyncService Usage Example

## Service Usage

```csharp
// Inject the service (already registered in Program.cs)
public class BackgroundSyncService(IPlaylistSyncService playlistSyncService)
{
    public async Task SyncChannelPlaylistsAsync(string channelId, CancellationToken cancellationToken)
    {
        try 
        {
            var result = await playlistSyncService.SyncAsync(channelId, cancellationToken);
            
            Console.WriteLine($"Sync completed for channel {channelId}:");
            Console.WriteLine($"  Videos: {result.VideosInserted} inserted, {result.VideosUpdated} updated");
            Console.WriteLine($"  Playlists: {result.PlaylistsUpserted} upserted");
            Console.WriteLine($"  Memberships: {result.MembershipsAdded} added, {result.MembershipsRemoved} removed");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Channel validation error: {ex.Message}");
        }
    }
}
```

## Database Verification Queries

```sql
-- Check playlists for a channel
SELECT PlaylistId, Title, UpdatedAt, LastMembershipSyncAt 
FROM Playlists 
WHERE ChannelId = 'your-channel-id';

-- Check playlist memberships
SELECT vp.PlaylistId, p.Title, COUNT(*) as VideoCount
FROM VideoPlaylists vp
JOIN Playlists p ON vp.PlaylistId = p.PlaylistId
GROUP BY vp.PlaylistId, p.Title;

-- Check sync state
SELECT ChannelId, LastUploadsCutoff 
FROM VideoSyncStates;

-- Verify referential integrity
SELECT COUNT(*) FROM VideoPlaylists vp
LEFT JOIN Videos v ON vp.VideoId = v.VideoId
WHERE v.VideoId IS NULL; -- Should return 0
```

## Key Benefits Demonstrated

1. **Delta Sync**: Second run will process minimal data due to cutoff tracking
2. **FK Safety**: VideoPlaylists only references existing videos
3. **Idempotent**: Multiple runs produce consistent state
4. **Batch Efficiency**: Large playlists processed in manageable chunks
5. **API Optimization**: Early termination prevents unnecessary API calls