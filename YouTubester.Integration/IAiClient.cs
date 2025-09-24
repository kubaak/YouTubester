namespace YouTubester.Integration;

public interface IAiClient
{
    Task<(string? Title, string? Description)> SuggestMetadataAsync(string currentTitle, string currentDescription, IEnumerable<string> tags, CancellationToken ct = default);
    Task<string?> SuggestReplyAsync(string videoTitle, IEnumerable<string> tags, string commentText, CancellationToken ct = default);
}