using YouTubester.Domain;
using Microsoft.EntityFrameworkCore;

namespace YouTubester.Persistence;

public class CommentRepository(YouTubesterDb db) : ICommentRepository
{
    public async Task<IEnumerable<ReplyDraft>> GetDraftsAsync()
        => await db.Drafts.AsNoTracking().OrderByDescending(d => d.UpdatedAt).ToListAsync();

    public async Task<ReplyDraft?> GetDraftAsync(string commentId)
        => await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.CommentId == commentId);

    public async Task AddOrUpdateDraftAsync(ReplyDraft draft)
    {
        var existing = await db.Drafts.FirstOrDefaultAsync(d => d.CommentId == draft.CommentId);
        if (existing is null)
        {
            db.Drafts.Add(draft);
        }
        else
        {
            existing.FinalText  = draft.FinalText ?? existing.FinalText;
            existing.Suggested  = draft.Suggested;
            existing.Approved   = draft.Approved;
            existing.UpdatedAt  = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteDraftAsync(string commentId)
    {
        var entity = await db.Drafts.FirstOrDefaultAsync(d => d.CommentId == commentId);
        if (entity != null)
        {
            db.Drafts.Remove(entity);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> HasPostedAsync(string commentId)
        => await db.PostedReplies.AnyAsync(r => r.CommentId == commentId);

    public async Task AddPostedAsync(PostedReply reply)
    {
        db.PostedReplies.Add(reply);
        await db.SaveChangesAsync();
    }
}

