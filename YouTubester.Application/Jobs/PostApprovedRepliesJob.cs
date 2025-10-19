using Hangfire;
using YouTubester.Integration;
using YouTubester.Persistence;
using YouTubester.Persistence.Replies;

namespace YouTubester.Application.Jobs;

public sealed class PostApprovedRepliesJob(
    IReplyRepository repository,
    IYouTubeIntegration youTubeIntegration
    )
{
    [Queue("replies")]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task Run(string commentId, IJobCancellationToken jobCancellationToken)
    {
        jobCancellationToken.ThrowIfCancellationRequested();
        var draft = await repository.GetReplyAsync(commentId, jobCancellationToken.ShutdownToken) ?? throw new ArgumentException($"The draft {commentId} could not be found.");

        //todo transaction
        await youTubeIntegration.ReplyAsync(commentId, draft.FinalText!, jobCancellationToken.ShutdownToken);
        draft.Post(DateTimeOffset.Now);//todo provider
        await repository.AddOrUpdateReplyAsync(draft, jobCancellationToken.ShutdownToken);
    }
}