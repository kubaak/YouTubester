using YouTubester.Application.Contracts;
using YouTubester.Application.Contracts.Replies;
using YouTubester.Domain;

namespace YouTubester.Application;

public interface IReplyService
{
    Task<IEnumerable<Reply>> GetRepliesForApprovalAsync(CancellationToken cancellationToken);
    Task<Reply?> DeleteAsync(string commentId, CancellationToken cancellationToken);

    Task<BatchDecisionResultDto> ApplyBatchAsync(IEnumerable<DraftDecisionDto> decisions,
        CancellationToken cancellationToken);

    Task<BatchIgnoreResult> IgnoreBatchAsync(string[] commentIds, CancellationToken ct);

    /// <summary>
    /// Gets a paginated list of replies with optional status filtering.
    /// </summary>
    /// <param name="statuses">Optional array of status filters.</param>
    /// <param name="pageSize">Number of items per page (1-100, defaults to configured DefaultPageSize).</param>
    /// <param name="pageToken">Cursor token for pagination, or null for first page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated result containing replies and optional next page token.</returns>
    /// <exception cref="Exceptions.InvalidPageSizeException">When pageSize is outside valid range.</exception>
    /// <exception cref="Exceptions.InvalidPageTokenException">When pageToken is malformed.</exception>
    Task<PagedResult<ReplyListItemDto>> GetRepliesAsync(ReplyStatus[]? statuses, int? pageSize, string? pageToken, CancellationToken ct);
}
