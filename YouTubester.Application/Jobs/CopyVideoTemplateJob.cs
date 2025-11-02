using Hangfire;

namespace YouTubester.Application.Jobs;

public sealed class CopyVideoTemplateJob(IVideoTemplatingService videoTemplatingService)
{
    [Queue("templating")]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task Run(CopyVideoTemplateRequest req, IJobCancellationToken jobToken)
    {
        jobToken.ThrowIfCancellationRequested();
        await videoTemplatingService.CopyTemplateAsync(req, jobToken.ShutdownToken);
    }
}