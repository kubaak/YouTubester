using YouTubester.Domain;

namespace YouTubester.Persistence;

public interface ICommentRepository
{
    Task<IEnumerable<ReplyDraft>> GetDraftsAsync();
    Task<ReplyDraft?> GetDraftAsync(string commentId);
    Task AddOrUpdateDraftAsync(ReplyDraft draft);
    Task DeleteDraftAsync(string commentId);

    Task<bool> HasPostedAsync(string commentId);
    Task AddPostedAsync(PostedReply reply);
}