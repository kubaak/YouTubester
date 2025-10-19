using YouTubester.Integration;

namespace YouTubester.Application;

public sealed class VideoTemplatingService(IYouTubeIntegration youTubeIntegration, IAiClient aiClient)
    : IVideoTemplatingService
{
    public async Task<CopyVideoTemplateResult> CopyTemplateAsync(
        CopyVideoTemplateRequest request, CancellationToken cancellationToken)
    {
        var sourceId = ParseYouTubeVideoId(request.SourceUrl);
        var targetId = ParseYouTubeVideoId(request.TargetUrl);

        var source = await youTubeIntegration.GetVideoDetailsAsync(sourceId, cancellationToken) ?? throw new ArgumentException($"Source {sourceId} not found.");
        var target = await youTubeIntegration.GetVideoDetailsAsync(targetId, cancellationToken) ?? throw new ArgumentException($"Target {targetId} not found.");

        var newTitle = source.Title;
        var newDescription = source.Description;
        var newTags = request.CopyTags ? SanitizeTags(source.Tags) : target.Tags;

        if (request.AiSuggestionOptions is not null)
        {
            var (suggestedTitle, suggestedDescription, tags) = await aiClient.SuggestMetadataAsync(
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
                newTags = tags.ToArray();
            }
        }

        var location = request.CopyLocation ? source.Location : target.Location;
        var locationDescription = request.CopyLocation ? source.LocationDescription : target.LocationDescription;
        var categoryId = request.CopyCategory ? source.CategoryId : target.CategoryId;
        var defaultLanguage = request.CopyDefaultLanguages ? source.DefaultLanguage : target.DefaultLanguage;
        var defaultAudioLanguage = request.CopyDefaultLanguages ? source.DefaultAudioLanguage : target.DefaultAudioLanguage;

        // 1) Update target video metadata
        await youTubeIntegration.UpdateVideoAsync(targetId, newTitle, newDescription, newTags, categoryId,
            defaultLanguage, defaultAudioLanguage, location, locationDescription, cancellationToken);

        // 2) Copy playlist membership (add-only, no removals)
        var added = new List<string>();
        if (request.CopyPlaylists)
        {
            var sourcePlaylists = await youTubeIntegration.GetPlaylistsContainingAsync(sourceId, cancellationToken);
            if (sourcePlaylists.Count > 0)
            {
                foreach (var playlistId in sourcePlaylists)
                {
                    await youTubeIntegration.AddVideoToPlaylistAsync(playlistId, targetId, cancellationToken);
                    added.Add(playlistId);
                }
            }
        }

        return new CopyVideoTemplateResult(
            sourceId, targetId, newTitle, newDescription, newTags, locationDescription, location,
            added, request.CopyCategory, request.CopyDefaultLanguages
            );
    }

    private static string ParseYouTubeVideoId(string urlOrId)
    {
        // Accept raw IDs and URLs; keep this liberal but robust
        var s = urlOrId.Trim();
        if (s.Length == 11 && !s.Contains('/'))
        {
            return s;  // likely already an ID
        }

        // common patterns: https://www.youtube.com/watch?v=VIDEOID, youtu.be/VIDEOID, plus lists & params
        var uri = new Uri(s, UriKind.Absolute);
        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            var id = uri.AbsolutePath.Trim('/');  // /VIDEOID
            if (id.Length == 11)
            {
                return id;
            }
        }
        if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            var q = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var id = q["v"];
            if (!string.IsNullOrEmpty(id) && id.Length == 11)
            {
                return id;
            }
        }
        throw new ArgumentException("Could not parse YouTube video id from url.");
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