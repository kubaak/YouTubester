using Hangfire;

namespace YouTubester.Application.Jobs;

public sealed class CopyVideoTemplateJob(IVideoTemplatingService videoTemplatingService)
{
    [Queue("templating")]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task Run(string userId, CopyVideoTemplateRequest req, IJobCancellationToken jobToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        jobToken.ThrowIfCancellationRequested();
        await videoTemplatingService.CopyTemplateAsync(userId, req, jobToken.ShutdownToken);
    }
}
