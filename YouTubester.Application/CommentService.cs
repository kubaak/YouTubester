using Hangfire;
using YouTubester.Application.Contracts;
using YouTubester.Application.Jobs;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence;

namespace YouTubester.Application;

public partial class CommentService(
    ICommentRepository repo, IYouTubeIntegration youTubeIntegration, IAiClient ai,
    IBackgroundJobClient backgroundJobClient) : ICommentService
{
    public Task<IEnumerable<Reply>> GetDraftsAsync() => repo.GetDraftsAsync();
    public async Task GeDeleteAsync(string commentId, CancellationToken cancellationToken)
    {
        await repo.DeleteDraftAsync(commentId, cancellationToken);
    }
    
    public async Task<int> ScanAndSuggestReplyAsync(int maxDrafts, CancellationToken ct = default)
    {
        var drafted = 0;
        await foreach (var vid in youTubeIntegration.GetAllPublicVideoIdsAsync(ct))
        {
            var video = await youTubeIntegration.GetVideoAsync(vid, ct);
            if (video is null || !video.IsPublic) continue;

            await foreach (var c in youTubeIntegration.GetUnansweredTopLevelCommentsAsync(vid, ct))
            {
                if (drafted >= maxDrafts) return drafted;
                var draft = await repo.GetDraftAsync(c.ParentCommentId);
                if (draft is not null) continue;
                if (draft!.PostedAt is not null) continue;
                

                var suggestedReply = string.IsNullOrWhiteSpace(c.Text) || !MyRegex().IsMatch(c.Text)
                    ? "🔥🙌"
                    : (await ai.SuggestReplyAsync(video.Title, video.Tags, c.Text, ct) ?? "Thanks for the comment! 🙌");

                var reply = new Reply
                {
                    CommentId = c.ParentCommentId,
                    VideoId = c.VideoId,
                    VideoTitle = video.Title,
                    CommentText = c.Text,
                    Suggested = suggestedReply,
                };
                await repo.AddOrUpdateDraftAsync(reply);
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

                if (string.IsNullOrWhiteSpace(d.ApprovedText))
                {
                    results.Add(new(d.CommentId, false, "Draft is empty"));
                    fail++;
                    continue;
                }
                
                var amended = d.ApprovedText.Trim();
                if (amended.Length > 320) amended = amended[..320]; // YouTube reply cap safety
                draft.FinalText = amended;
                draft.Approve();
                //todo schedule to prevent the rate limits
                backgroundJobClient.Enqueue<PostApprovedCommentsJob>(j => j.RunOne(draft.CommentId, ct));
                draft.Schedule(DateTimeOffset.Now);
                
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

    [System.Text.RegularExpressions.GeneratedRegex(@"\p{L}|\p{N}")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}