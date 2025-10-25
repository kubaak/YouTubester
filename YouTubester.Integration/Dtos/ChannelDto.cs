namespace YouTubester.Integration.Dtos;

public sealed record ChannelDto(
    string Id,
    string Name,
    string UploadsPlaylistId,
    string? ETag
);