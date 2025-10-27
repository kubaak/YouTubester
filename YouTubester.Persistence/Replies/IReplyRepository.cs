using YouTubester.Domain;

namespace YouTubester.Persistence.Replies;

public interface IReplyRepository
{
    Task<IEnumerable<Reply>> GetRepliesForApprovalAsync(CancellationToken cancellationToken);
    Task<Reply?> GetReplyAsync(string commentId, CancellationToken cancellationToken);
    Task AddOrUpdateReplyAsync(Reply reply, CancellationToken cancellationToken);
    Task<Reply?> DeleteReplyAsync(string commentId, CancellationToken cancellationToken);
    Task<List<(string CommentId, ReplyStatus Status)>> LoadStatusesAsync(IEnumerable<string> ids, CancellationToken ct);
    Task<string[]> IgnoreManyAsync(IEnumerable<string> ids, CancellationToken ct);

    /// <summary>
    /// Gets a page of replies with optional status filtering and cursor-based pagination.
    /// </summary>
    /// <param name="statuses">Optional set of statuses to include.</param>
    /// <param name="afterPulledAtUtc">Cursor: pulled date to search after (exclusive).</param>
    /// <param name="afterCommentId">Cursor: comment ID to search after when pulled dates are equal.</param>
    /// <param name="take">Maximum number of items to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of replies ordered by PulledAt DESC, CommentId DESC.</returns>
    Task<List<Reply>> GetRepliesPageAsync(IReadOnlyCollection<ReplyStatus>? statuses, DateTimeOffset? afterPulledAtUtc, string? afterCommentId, int take, CancellationToken ct);
}
