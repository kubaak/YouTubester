namespace YouTubester.Domain;

public class PostedReply
{
    public string CommentId { get; set; } = default!;
    public string VideoId { get; set; } = default!;
    public string ReplyText { get; set; } = default!;
    public DateTimeOffset PostedAt { get; set; } = DateTimeOffset.UtcNow;
}