using YouTubester.Domain;

namespace YouTubester.Application;

public interface ICommentService
{
    Task<IEnumerable<ReplyDraft>> GetDraftsAsync();
    Task ApproveDraftAsync(string commentId);
    Task PostReplyAsync(string commentId);
    Task<int> ScanAndDraftAsync(int maxDrafts, CancellationToken ct = default);
}