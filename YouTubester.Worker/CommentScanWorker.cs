using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence;
using YouTubester.Persistence.Replies;

namespace YouTubester.Worker;

public partial  class CommentScanWorker(
    ILogger<CommentScanWorker> log, 
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> opt)
    : BackgroundService
{
    private readonly WorkerOptions _opt = opt.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("CommentScanWorker started. Interval: {S}s", _opt.IntervalSeconds);
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(_opt.IntervalSeconds));

        do
        {
            try
            {
                var count = await ScanOnceAsync(stoppingToken);
                log.LogInformation("Scan completed. Drafted: {Count}", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Scan failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private static bool IsEmojiOnly(string text) => !MyRegex().IsMatch(text);

    private async Task<int> ScanOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var yt   = scope.ServiceProvider.GetRequiredService<IYouTubeIntegration>();
        var ai   = scope.ServiceProvider.GetRequiredService<IAiClient>();
        var repo = scope.ServiceProvider.GetRequiredService<IReplyRepository>();

        var drafted = 0;
        //todo load from the cache instead
        await foreach (var video in yt.GetAllVideosAsync(null, cancellationToken))
        {
            if (drafted >= _opt.MaxDraftsPerRun) break;


            if (!video.IsPublic) continue;

            await foreach (var thread in yt.GetUnansweredTopLevelCommentsAsync(video.VideoId, cancellationToken))
            {
                if (drafted >= _opt.MaxDraftsPerRun) break;

                var existingReply = await repo.GetReplyAsync(thread.ParentCommentId, cancellationToken);
                // Skip if we've already pulled this
                if (existingReply is not null) continue;

                string replyText;
                if (IsEmojiOnly(thread.Text))
                {
                    replyText = "🔥🙌";
                }
                else
                {
                    var suggestion = await ai.SuggestReplyAsync(
                        video.Title,
                        video.Tags,
                        thread.Text,
                        cancellationToken);

                    replyText = string.IsNullOrWhiteSpace(suggestion)
                        ? "Thanks for the comment! 🙌"
                        : suggestion;
                }
                
                var reply = Reply.Create(thread.ParentCommentId, thread.VideoId, video.Title, thread.Text,
                    DateTimeOffset.Now);
                reply.SuggestText(replyText, DateTimeOffset.Now);

                await repo.AddOrUpdateReplyAsync(reply, cancellationToken);

                drafted++;
            }
        }
        return drafted;
    }

    [GeneratedRegex(@"\p{L}|\p{N}")]
    private static partial Regex MyRegex();
}