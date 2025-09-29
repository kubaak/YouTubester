using Hangfire;
using YouTubester.Application.Contracts;
using YouTubester.Application.Jobs;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence;

namespace YouTubester.Application;

public class CommentService(
    IReplyRepository repo, IYouTubeIntegration youTubeIntegration, IAiClient ai,
    IBackgroundJobClient backgroundJobClient) : ICommentService
{
    public Task<IEnumerable<Reply>> GetDraftsAsync(CancellationToken cancellationToken) 
        => repo.GetRepliesAsync(cancellationToken);
    
    public async Task GeDeleteAsync(string commentId, CancellationToken cancellationToken)
    {
        await repo.DeleteReplyAsync(commentId, cancellationToken);
    }

    public async Task<BatchDecisionResultDto> ApplyBatchAsync(
        IEnumerable<DraftDecisionDto> decisions, CancellationToken cancellationToken)
    {
        var results = new List<DraftDecisionResultDto>();
        int ok = 0, fail = 0;

        foreach (var d in decisions)
        {
            try
            {
                var draft = await repo.GetReplyAsync(d.CommentId, cancellationToken);
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
                
                draft.ApproveText(d.ApprovedText, DateTimeOffset.Now);
                //todo schedule to prevent the rate limits
                backgroundJobClient.Enqueue<PostApprovedCommentsJob>(j => j.RunOne(draft.CommentId, cancellationToken));
                
                await repo.AddOrUpdateReplyAsync(draft, cancellationToken);
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