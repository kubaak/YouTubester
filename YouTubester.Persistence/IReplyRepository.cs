using YouTubester.Domain;

namespace YouTubester.Persistence;

public interface IReplyRepository
{
    Task<IEnumerable<Reply>> GetRepliesAsync(CancellationToken cancellationToken);
    Task<Reply?> GetReplyAsync(string commentId, CancellationToken cancellationToken);
    Task AddOrUpdateReplyAsync(Reply reply, CancellationToken cancellationToken);
    Task DeleteReplyAsync(string commentId, CancellationToken cancellationToken);
}