using Hangfire;
using YouTubester.Application.Contracts;
using YouTubester.Application.Jobs;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence;

namespace YouTubester.Application;

public class ReplyService(IReplyRepository repository, IBackgroundJobClient backgroundJobClient) 
    : IReplyService
{
    public Task<IEnumerable<Reply>> GetRepliesForApprovalAsync(CancellationToken cancellationToken) 
        => repository.GetRepliesForApprovalAsync(cancellationToken);
    
    public async Task<Reply?> DeleteAsync(string commentId, CancellationToken cancellationToken)
    {
        return await repository.DeleteReplyAsync(commentId, cancellationToken);
    }

    public async Task<Reply?> IgnoreAsync(string commentId, CancellationToken cancellationToken)
    {
        var reply = await repository.GetReplyAsync(commentId, cancellationToken);
        if (reply is not null)
        {
            reply.Ignore();
            await repository.AddOrUpdateReplyAsync(reply, cancellationToken);
        }
        
        return reply;
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
                var draft = await repository.GetReplyAsync(d.CommentId, cancellationToken);
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
                //todo from configuration
                backgroundJobClient.Schedule<PostApprovedRepliesJob>(j => j.Run(draft.CommentId, JobCancellationToken.Null),TimeSpan.FromSeconds(10));
                
                await repository.AddOrUpdateReplyAsync(draft, cancellationToken);
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