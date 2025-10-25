namespace YouTubester.Application.Channels;

public sealed record ChannelSyncResult(
    int VideosInserted,
    int VideosUpdated,
    int PlaylistsUpserted,
    int MembershipsAdded,
    int MembershipsRemoved);