namespace YouTubester.Integration.Dtos;

public sealed record VideoDto(
    string Id,
    string Title,
    string[] Tags,
    TimeSpan Duration,
    bool IsPublic,
    bool IsShort
);