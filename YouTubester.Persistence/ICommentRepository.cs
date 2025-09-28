using YouTubester.Domain;

namespace YouTubester.Persistence;

public interface ICommentRepository
{
    Task<IEnumerable<Reply>> GetDraftsAsync();
    Task<Reply?> GetDraftAsync(string commentId);
    Task AddOrUpdateDraftAsync(Reply reply);
    Task DeleteDraftAsync(string commentId, CancellationToken cancellationToken);
}