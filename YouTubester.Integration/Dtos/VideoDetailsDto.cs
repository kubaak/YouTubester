namespace YouTubester.Integration.Dtos;

[Obsolete("Use domain video instead")]
public sealed record VideoDetailsDto(
    string Id,
    string Title,
    string Description,
    string[] Tags,
    string? CategoryId,
    string? DefaultLanguage,
    string? DefaultAudioLanguage,
    (double lat, double lng)? Location,
    string? LocationDescription
);