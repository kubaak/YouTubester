using YouTubester.Domain;
using Microsoft.EntityFrameworkCore;

namespace YouTubester.Persistence;

public class CommentRepository(YouTubesterDb db) : ICommentRepository
{
    public async Task<IEnumerable<Reply>> GetDraftsAsync()
        => await db.Drafts.AsNoTracking().OrderByDescending(d => d.PostedAt).ToListAsync();

    public async Task<Reply?> GetDraftAsync(string commentId)
        => await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.CommentId == commentId);

    public async Task AddOrUpdateDraftAsync(Reply reply)
    {
        var existing = await db.Drafts.FirstOrDefaultAsync(d => d.CommentId == reply.CommentId);
        if (existing is null)
        {
            db.Drafts.Add(reply);
        }
        else
        {
            existing.FinalText  = reply.FinalText ?? existing.FinalText;
            existing.Suggested  = reply.Suggested;
            existing.Approved   = reply.Approved;
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteDraftAsync(string commentId, CancellationToken cancellationToken)
    {
        var entity = await db.Drafts.FirstOrDefaultAsync(d => d.CommentId == commentId, cancellationToken);
        if (entity != null)
        {
            db.Drafts.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<Reply> GetDraft(string commentId, CancellationToken cancellationToken)
    {
        return db.Drafts.SingleAsync(d => d.CommentId == commentId, cancellationToken);
    }
}

