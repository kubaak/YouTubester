using YouTubester.Application.Contracts;
using YouTubester.Application.Contracts.Replies;
using YouTubester.Domain;

namespace YouTubester.Application;

public interface IReplyService
{
    Task<IEnumerable<Reply>> GetRepliesForApprovalAsync(CancellationToken cancellationToken);
    Task<Reply?> DeleteAsync(string commentId, CancellationToken cancellationToken);
    Task<BatchDecisionResultDto> ApplyBatchAsync(IEnumerable<DraftDecisionDto> decisions, CancellationToken cancellationToken);
    Task<BatchIgnoreResult> IgnoreBatchAsync(string[] commentIds, CancellationToken ct);
}