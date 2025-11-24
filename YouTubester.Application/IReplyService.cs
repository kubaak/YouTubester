using YouTubester.Application.Contracts.Replies;
using YouTubester.Domain;

namespace YouTubester.Application;

public interface IReplyService
{
    Task<IEnumerable<Reply>> GetRepliesForApprovalAsync(CancellationToken cancellationToken);
    Task<Reply?> DeleteAsync(string commentId, CancellationToken cancellationToken);

    Task<BatchDecisionResultDto> ApplyBatchAsync(
        string userId,
        IEnumerable<DraftDecisionDto> decisions,
        CancellationToken cancellationToken);

    Task<BatchIgnoreResult> IgnoreBatchAsync(string[] commentIds, CancellationToken ct);
}
