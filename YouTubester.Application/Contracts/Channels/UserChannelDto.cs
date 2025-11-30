namespace YouTubester.Application.Contracts.Channels;

public sealed record UserChannelDto(
    string Id,
    string Title,
    string? Picture
);