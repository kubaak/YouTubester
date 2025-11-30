namespace YouTubester.Abstractions.Channels;

public sealed record ChannelDto(
    string Id,
    string Name,
    string UploadsPlaylistId,
    string? ETag
);