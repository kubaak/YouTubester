﻿using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;

namespace YouTubester.Persistence.Replies;

public class ReplyRepository(YouTubesterDb db) : IReplyRepository
{
    public async Task<IEnumerable<Reply>> GetRepliesForApprovalAsync(CancellationToken cancellationToken)
        => await db.Replies.AsNoTracking()
            .Where(r => r.Status == ReplyStatus.Suggested)
            .OrderByDescending(d => d.PostedAt).
            ToListAsync(cancellationToken);

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

    public async Task<Reply?> DeleteReplyAsync(string commentId, CancellationToken cancellationToken)
    {
        var entity = await db.Replies
            .FirstOrDefaultAsync(r => r.CommentId == commentId, cancellationToken);

        if (entity is not null)
        {
            db.Replies.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
        return entity;
    }

    public async Task<List<(string CommentId, ReplyStatus Status)>> LoadStatusesAsync(IEnumerable<string> ids, CancellationToken ct)
        => await db.Replies.AsNoTracking()
            .Where(r => ids.Contains(r.CommentId))
            .Select(r => new ValueTuple<string, ReplyStatus>(r.CommentId, r.Status))
            .ToListAsync(ct);

    public async Task<string[]> IgnoreManyAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var list = ids.ToArray();
        if (list.Length == 0)
        {
            return [];
        }

        _ = await db.Replies
            .Where(r => list.Contains(r.CommentId))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, _ => ReplyStatus.Ignored), ct);
        return list;
    }
}

