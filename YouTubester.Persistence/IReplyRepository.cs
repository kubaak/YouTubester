using YouTubester.Domain;

namespace YouTubester.Persistence;

public interface IReplyRepository
{
    Task<IEnumerable<Reply>> GetRepliesForApprovalAsync(CancellationToken cancellationToken);
    Task<Reply?> GetReplyAsync(string commentId, CancellationToken cancellationToken);
    Task AddOrUpdateReplyAsync(Reply reply, CancellationToken cancellationToken);
    Task<Reply?> DeleteReplyAsync(string commentId, CancellationToken cancellationToken);
    Task<List<(string CommentId, ReplyStatus Status)>> LoadStatusesAsync(IEnumerable<string> ids, CancellationToken ct);
    Task<string[]> IgnoreManyAsync(IEnumerable<string> ids, CancellationToken ct);
}