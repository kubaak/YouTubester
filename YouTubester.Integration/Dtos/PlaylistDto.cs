namespace YouTubester.Integration.Dtos;

public sealed record PlaylistDto(
    string Id,
    string? Title,
    string? ETag
);