using Hangfire;
using YouTubester.Abstractions.Replies;
using YouTubester.Integration;

namespace YouTubester.Application.Jobs;

public sealed class PostApprovedRepliesJob(
    IReplyRepository repository,
    IYouTubeIntegration youTubeIntegration)
{
    [Queue("replies")]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task Run(string userId, string commentId, IJobCancellationToken jobCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        jobCancellationToken.ThrowIfCancellationRequested();
        var draft = await repository.GetReplyAsync(commentId, jobCancellationToken.ShutdownToken) ??
                    throw new ArgumentException($"The draft {commentId} could not be found.");

        //todo transaction
        await youTubeIntegration.ReplyAsync(commentId, draft.FinalText!, jobCancellationToken.ShutdownToken);
        draft.Post(DateTimeOffset.Now); //todo provider
        await repository.AddOrUpdateReplyAsync(draft, jobCancellationToken.ShutdownToken);
    }
}
