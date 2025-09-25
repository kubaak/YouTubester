using YouTubester.Application.Contracts;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence;

namespace YouTubester.Application;

public class CommentService(
    ICommentRepository repo, IYouTubeIntegration youTubeIntegration, IAiClient ai) : ICommentService
{
    public Task<IEnumerable<ReplyDraft>> GetDraftsAsync() => repo.GetDraftsAsync();
    public Task PostApprovedAsync(int maxToPost, int paceMs, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public async Task ApproveDraftAsync(string commentId)
    {
        var draft = await repo.GetDraftAsync(commentId);
        if (draft is null) throw new KeyNotFoundException("Draft not found");

        draft.Approved = true;
        draft.FinalText ??= draft.Suggested;
        draft.UpdatedAt = DateTimeOffset.UtcNow;

        await repo.AddOrUpdateDraftAsync(draft);
    }

    public async Task PostReplyAsync(string commentId)
    {
        var draft = await repo.GetDraftAsync(commentId)
                    ?? throw new KeyNotFoundException("Draft not found");
        
        var replyText = draft.FinalText ?? draft.Suggested;
        
        await youTubeIntegration.ReplyAsync(commentId, replyText);
        await repo.AddPostedAsync(new PostedReply
        {
            CommentId = draft.CommentId,
            VideoId = draft.VideoId,
            ReplyText = replyText,
            PostedAt = DateTimeOffset.UtcNow
        });
    }
    
    public async Task<int> ScanAndDraftAsync(int maxDrafts, CancellationToken ct = default)
    {
        var drafted = 0;
        await foreach (var vid in youTubeIntegration.GetAllPublicVideoIdsAsync(ct))
        {
            var video = await youTubeIntegration.GetVideoAsync(vid, ct);
            if (video is null || !video.IsPublic) continue;

            await foreach (var c in youTubeIntegration.GetUnansweredTopLevelCommentsAsync(vid, ct))
            {
                if (drafted >= maxDrafts) return drafted;
                if (await repo.HasPostedAsync(c.ParentCommentId)) continue;
                if (await repo.GetDraftAsync(c.ParentCommentId) is not null) continue;

                var reply = string.IsNullOrWhiteSpace(c.Text) || !System.Text.RegularExpressions.Regex.IsMatch(c.Text, @"\p{L}|\p{N}")
                    ? "🔥🙌"
                    : (await ai.SuggestReplyAsync(video.Title, video.Tags, c.Text, ct) ?? "Thanks for the comment! 🙌");

                await repo.AddOrUpdateDraftAsync(new ReplyDraft {
                    CommentId = c.ParentCommentId,
                    VideoId = c.VideoId,
                    VideoTitle = video.Title,
                    CommentText = c.Text,
                    Suggested = reply,
                    Approved = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                drafted++;
            }
        }
        return drafted;
    }

    public async Task<BatchDecisionResultDto> ApplyBatchAsync(
        IEnumerable<DraftDecisionDto> decisions,
        CancellationToken ct = default)
    {
        var results = new List<DraftDecisionResultDto>();
        int ok = 0, fail = 0;

        foreach (var d in decisions)
        {
            try
            {
                var draft = await repo.GetDraftAsync(d.CommentId);
                if (draft is null)
                {
                    results.Add(new(d.CommentId, false, "Draft not found"));
                    fail++;
                    continue;
                }

                // optional amend
                if (!string.IsNullOrWhiteSpace(d.NewText))
                {
                    var amended = d.NewText!.Trim();
                    if (amended.Length > 320) amended = amended[..320]; // YouTube reply cap safety
                    draft.FinalText = amended;
                    draft.UpdatedAt = DateTimeOffset.UtcNow;
                }

                // approval via batch only
                if (d.Approve)
                {
                    draft.Approved = true;
                    draft.FinalText ??= draft.Suggested;
                    draft.UpdatedAt = DateTimeOffset.UtcNow;
                }

                await repo.AddOrUpdateDraftAsync(draft);
                results.Add(new(d.CommentId, true));
                ok++;
            }
            catch (Exception ex)
            {
                results.Add(new(d.CommentId, false, ex.Message));
                fail++;
            }
        }

        return new BatchDecisionResultDto(
            Total: ok + fail,
            Succeeded: ok,
            Failed: fail,
            Items: results);
    }
}