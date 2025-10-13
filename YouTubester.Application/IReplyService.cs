using YouTubester.Application.Contracts;
using YouTubester.Domain;

namespace YouTubester.Application;

public interface IReplyService
{
    Task<IEnumerable<Reply>> GetRepliesForApprovalAsync(CancellationToken cancellationToken);
    Task<Reply?> DeleteAsync(string commentId, CancellationToken cancellationToken);
    Task<Reply?> IgnoreAsync(string commentId, CancellationToken cancellationToken);
    Task<BatchDecisionResultDto> ApplyBatchAsync(IEnumerable<DraftDecisionDto> decisions, CancellationToken cancellationToken);
}