namespace YouTubester.Application.Contracts.Channels;

public sealed record AvailableChannelDto(
    string Id,
    string Name,
    string UploadsPlaylistId,
    string? ETag
);
