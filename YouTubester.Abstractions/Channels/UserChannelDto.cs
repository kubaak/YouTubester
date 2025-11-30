namespace YouTubester.Abstractions.Channels;

public sealed record UserChannelDto(
    string Id,
    string Title,
    string? Picture
);