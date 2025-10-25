using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence;
using YouTubester.Persistence.Videos;

namespace YouTubester.Application;

public sealed class VideoTemplatingService(
    IYouTubeIntegration youTubeIntegration,
    IAiClient aiClient,
    IVideoRepository videoRepository,
    YouTubesterDb dbContext)
    : IVideoTemplatingService
{
    public async Task<CopyVideoTemplateResult> CopyTemplateAsync(
        CopyVideoTemplateRequest request, CancellationToken cancellationToken)
    {
        // Load source and target videos from DB
        var sourceVideo = await videoRepository.GetVideoByIdAsync(request.SourceVideoId, cancellationToken)
            ?? throw new ArgumentException($"Source video {request.SourceVideoId} not found in cache.");

        var targetVideo = await videoRepository.GetVideoByIdAsync(request.TargetVideoId, cancellationToken)
            ?? throw new ArgumentException($"Target video {request.TargetVideoId} not found in cache.");

        // Build effective metadata starting from source
        var newTitle = sourceVideo.Title ?? string.Empty;
        var newDescription = sourceVideo.Description ?? string.Empty;
        var newTags = request.CopyTags ? SanitizeTags(sourceVideo.Tags) : targetVideo.Tags;
        var location = request.CopyLocation ? ConvertToLocationTuple(sourceVideo.Location) : ConvertToLocationTuple(targetVideo.Location);
        var locationDescription = request.CopyLocation ? sourceVideo.LocationDescription : targetVideo.LocationDescription;
        var categoryId = request.CopyCategory ? sourceVideo.CategoryId : targetVideo.CategoryId;
        var defaultLanguage = request.CopyDefaultLanguages ? sourceVideo.DefaultLanguage : targetVideo.DefaultLanguage;
        var defaultAudioLanguage = request.CopyDefaultLanguages ? sourceVideo.DefaultAudioLanguage : targetVideo.DefaultAudioLanguage;

        // Apply AI suggestions if provided
        if (request.AiSuggestionOptions is not null)
        {
            var (suggestedTitle, suggestedDescription, suggestedTags) = await aiClient.SuggestMetadataAsync(
                request.AiSuggestionOptions.PromptEnrichment, cancellationToken);

            if (request.AiSuggestionOptions.GenerateTitle)
            {
                newTitle = suggestedTitle;
            }

            if (request.AiSuggestionOptions.GenerateDescription)
            {
                newDescription = suggestedDescription;
            }

            if (request.AiSuggestionOptions.GenerateTags)
            {
                newTags = suggestedTags.ToArray();
            }
        }

        // Update target video on YouTube
        await youTubeIntegration.UpdateVideoAsync(
            request.TargetVideoId,
            newTitle,
            newDescription,
            newTags,
            categoryId,
            defaultLanguage,
            defaultAudioLanguage,
            location,
            locationDescription,
            cancellationToken);

        // Persist changes to DB only after successful YouTube update
        var nowUtc = DateTimeOffset.UtcNow;
        var targetVideoEntity = await dbContext.Videos.FirstOrDefaultAsync(v => v.VideoId == request.TargetVideoId, cancellationToken);

        if (targetVideoEntity is not null)
        {
            targetVideoEntity.ApplyDetails(
                newTitle,
                newDescription,
                targetVideoEntity.PublishedAt, // Keep original published date
                targetVideoEntity.Duration, // Keep original duration
                targetVideoEntity.Visibility, // Keep original visibility
                newTags,
                categoryId,
                defaultLanguage,
                defaultAudioLanguage,
                ConvertFromLocationTuple(location),
                locationDescription,
                nowUtc,
                targetVideoEntity.ThumbnailUrl, // Keep original thumbnail
                targetVideoEntity.ETag, // Keep original ETag
                targetVideoEntity.CommentsAllowed // Keep original comments setting
            );

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Handle playlist copying (simplified for now - no actual implementation needed per spec)
        var added = new List<string>();

        return new CopyVideoTemplateResult(
            request.SourceVideoId,
            request.TargetVideoId,
            newTitle,
            newDescription,
            newTags,
            locationDescription,
            location,
            added,
            request.CopyCategory,
            request.CopyDefaultLanguages
        );
    }

    private static (double lat, double lng)? ConvertToLocationTuple(GeoLocation? location)
    {
        return location is not null
            ? (location.Latitude, location.Longitude)
            : null;
    }

    private static GeoLocation? ConvertFromLocationTuple((double lat, double lng)? location)
    {
        return location.HasValue ? new GeoLocation(location.Value.lat, location.Value.lng) : null;
    }

    private static string[] SanitizeTags(IReadOnlyList<string> tags)
    {
        var cleaned = tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<string>();
        var total = 0;
        foreach (var tag in cleaned)
        {
            var add = tag.Length;
            if (total + add > 500)
            {
                break;
            }

            result.Add(tag);
            total += add;
        }
        return result.ToArray();
    }
}