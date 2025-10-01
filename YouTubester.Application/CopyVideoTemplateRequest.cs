namespace YouTubester.Application;

public sealed record CopyVideoTemplateRequest(
    string SourceUrl,
    string TargetUrl,
    bool CopyTags = false,
    bool CopyLocation = true,
    bool CopyPlaylists = true,
    bool CopyCategory = true,
    bool CopyDefaultLanguages = true,
    AiSuggestionOptions? AiSuggestionOptions = null
);

public sealed record AiSuggestionOptions(
    string PromptEnrichment,
    bool GenerateTitle = true,
    bool GenerateDescription = true,
    bool GenerateTags = true //Overrides CopyVideoTemplateRequest.CopyTags
);