namespace YouTubester.Domain;

public class Reply
{
    public string CommentId { get; set; } = default!;
    public string VideoId { get; set; } = default!;
    public string VideoTitle { get; set; } = default!;
    public string CommentText { get; set; } = default!;
    public string Suggested { get; set; } = default!;
    public string? FinalText { get; set; }
    public bool Approved { get; set; } 
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? Scheduled { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }

    public void Approve()
    {
        Approved = true;
    }
    public void Schedule(DateTimeOffset scheduledAt)
    {
        Scheduled = scheduledAt;
    }
    public void Post(DateTimeOffset postedAt)
    {
        PostedAt = postedAt;
    }
}