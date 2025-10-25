using YouTubester.Domain;

namespace YouTubester.Application.Contracts.Replies;

/// <summary>
/// DTO for reply list items in paginated responses.
/// </summary>
public sealed record ReplyListItemDto
{
    /// <summary>
    /// Unique identifier for the comment.
    /// </summary>
    public required string CommentId { get; init; }

    /// <summary>
    /// ID of the video this reply belongs to.
    /// </summary>
    public required string VideoId { get; init; }

    /// <summary>
    /// Title of the video this reply belongs to.
    /// </summary>
    public required string VideoTitle { get; init; }

    /// <summary>
    /// The original comment text.
    /// </summary>
    public required string CommentText { get; init; }

    /// <summary>
    /// Current status of the reply.
    /// </summary>
    public required ReplyStatus Status { get; init; }

    /// <summary>
    /// When the comment was initially pulled.
    /// </summary>
    public required DateTimeOffset PulledAt { get; init; }

    /// <summary>
    /// When the reply suggestion was generated (if applicable).
    /// </summary>
    public DateTimeOffset? SuggestedAt { get; init; }

    /// <summary>
    /// When the reply was approved (if applicable).
    /// </summary>
    public DateTimeOffset? ApprovedAt { get; init; }

    /// <summary>
    /// When the reply was posted (if applicable).
    /// </summary>
    public DateTimeOffset? PostedAt { get; init; }
}