namespace YouTubester.Integration.Dtos;

public sealed record VideoDto(
    string ChannelId,
    string VideoId,
    string Title,
    string Description,
    string[] Tags,
    TimeSpan Duration,
    string PrivacyStatus,
    bool IsShort,
    DateTimeOffset PublishedAt,
    string? CategoryId,
    string? DefaultLanguage,
    string? DefaultAudioLanguage,
    (double lat, double lng)? Location,
    string? LocationDescription
);