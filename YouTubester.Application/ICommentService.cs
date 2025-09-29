using YouTubester.Application.Contracts;
using YouTubester.Domain;

namespace YouTubester.Application;

public interface ICommentService
{
    Task<IEnumerable<Reply>> GetDraftsAsync(CancellationToken cancellationToken);
    Task GeDeleteAsync(string commentId, CancellationToken cancellationToken);
    Task<BatchDecisionResultDto> ApplyBatchAsync(IEnumerable<DraftDecisionDto> decisions, CancellationToken cancellationToken);
}