namespace YouTubester.Domain;

public class ReplyDraft
{
    public string CommentId { get; set; } = default!;
    public string VideoId { get; set; } = default!;
    public string VideoTitle { get; set; } = default!;
    public string CommentText { get; set; } = default!;
    public string Suggested { get; set; } = default!;
    public string? FinalText { get; set; }
    public bool Approved { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}