using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence;

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

    private async Task<int> ScanOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var yt   = scope.ServiceProvider.GetRequiredService<IYouTubeIntegration>();
        var ai   = scope.ServiceProvider.GetRequiredService<IAiClient>();
        var repo = scope.ServiceProvider.GetRequiredService<ICommentRepository>();

        var drafted = 0;
        await foreach (var videoId in yt.GetAllPublicVideoIdsAsync(ct))
        {
            if (drafted >= _opt.MaxDraftsPerRun) break;

            var video = await yt.GetVideoAsync(videoId, ct);
            if (video is null || !video.IsPublic) continue;

            await foreach (var thread in yt.GetUnansweredTopLevelCommentsAsync(videoId, ct))
            {
                if (drafted >= _opt.MaxDraftsPerRun) break;

                // Skip if we've already posted or drafted
                if (await repo.HasPostedAsync(thread.ParentCommentId)) continue;
                if (await repo.GetDraftAsync(thread.ParentCommentId) is not null) continue;

                string reply;
                if (IsEmojiOnly(thread.Text))
                {
                    reply = "🔥🙌";
                }
                else
                {
                    var suggestion = await ai.SuggestReplyAsync(
                        video.Title,
                        video.Tags,
                        thread.Text,
                        ct);

                    reply = string.IsNullOrWhiteSpace(suggestion)
                        ? "Thanks for the comment! 🙌"
                        : suggestion;
                }

                var now = DateTimeOffset.UtcNow;
                await repo.AddOrUpdateDraftAsync(new ReplyDraft
                {
                    CommentId = thread.ParentCommentId,
                    VideoId = thread.VideoId,
                    VideoTitle = video.Title,
                    CommentText = thread.Text,
                    Suggested = reply,
                    Approved = false,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                drafted++;
            }
        }
        return drafted;
    }

    [GeneratedRegex(@"\p{L}|\p{N}")]
    private static partial Regex MyRegex();
}