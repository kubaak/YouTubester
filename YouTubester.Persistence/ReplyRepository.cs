using YouTubester.Domain;
using Microsoft.EntityFrameworkCore;

namespace YouTubester.Persistence;

public class ReplyRepository(YouTubesterDb db) : IReplyRepository
{
    public async Task<IEnumerable<Reply>> GetRepliesAsync(CancellationToken cancellationToken)
        => await db.Replies.AsNoTracking().OrderByDescending(d => d.PostedAt).ToListAsync(cancellationToken);

    public async Task<Reply?> GetReplyAsync(string commentId, CancellationToken cancellationToken)
        => await db.Replies.AsNoTracking().FirstOrDefaultAsync(d => d.CommentId == commentId, cancellationToken);
    
    public async Task AddOrUpdateReplyAsync(Reply reply, CancellationToken ct)
    {
        var tracked = await db.Replies.FirstOrDefaultAsync(r => r.CommentId == reply.CommentId, ct);

        if (tracked is null)
        {
            db.Replies.Add(reply);
        }
        else
        {
            db.Entry(tracked).CurrentValues.SetValues(reply);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteReplyAsync(string commentId, CancellationToken cancellationToken)
    {
        var entity = await db.Replies
            .FirstOrDefaultAsync(r => r.CommentId == commentId, cancellationToken);

        if (entity is null) return;

        db.Replies.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

