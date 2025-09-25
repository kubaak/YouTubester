using YouTubester.Application.Contracts;
using YouTubester.Domain;

namespace YouTubester.Application;

public interface ICommentService
{
    Task<IEnumerable<ReplyDraft>> GetDraftsAsync();
    Task PostApprovedAsync(int maxToPost, int paceMs, CancellationToken ct);
    Task PostReplyAsync(string commentId);
    Task<int> ScanAndDraftAsync(int maxDrafts, CancellationToken ct = default);
    Task<BatchDecisionResultDto> ApplyBatchAsync(IEnumerable<DraftDecisionDto> decisions, CancellationToken ct = default);
}