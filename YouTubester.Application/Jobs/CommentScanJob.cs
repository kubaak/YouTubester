using System.Text.RegularExpressions;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YouTubester.Application;
using YouTubester.Domain;
using YouTubester.Integration;
using YouTubester.Persistence.Channels;
using YouTubester.Persistence.Replies;
using YouTubester.Persistence.Videos;

namespace YouTubester.Application.Jobs;

public sealed partial class CommentScanJob(
    ILogger<CommentScanJob> logger,
    IOptions<WorkerOptions> options,
    IYouTubeIntegration youTubeIntegration,
    IAiClient aiClient,
    IChannelRepository channelRepository,
    IVideoRepository videoRepository,
    IReplyRepository replyRepository)
{
    private readonly WorkerOptions _options = options.Value;

    [Queue("scanning")]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task Run(IJobCancellationToken jobCancellationToken)
    {
        jobCancellationToken.ThrowIfCancellationRequested();

        try
        {
            var count = await ScanOnceAsync(jobCancellationToken.ShutdownToken);
            logger.LogInformation("Comment scan completed. Drafted: {Count}", count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Comment scan failed");
            throw;
        }
    }

    private static bool IsEmojiOnly(string text)
    {
        return !MyRegex().IsMatch(text);
    }

    private async Task<int> ScanOnceAsync(CancellationToken cancellationToken)
    {
        var channels = await channelRepository.GetChannelsAsync(cancellationToken);
        var drafted = 0;

        foreach (var channel in channels)
        {
            var channelId = channel.ChannelId;

            foreach (var video in await videoRepository.GetAllVideosAsync(cancellationToken))
            {
                if (drafted >= _options.MaxDraftsPerRun)
                {
                    break;
                }

                if (video.Visibility != VideoVisibility.Public)
                {
                    continue;
                }

                await foreach (var thread in youTubeIntegration.GetUnansweredTopLevelCommentsAsync(channelId, video.VideoId,
                                   cancellationToken))
                {
                    if (drafted >= _options.MaxDraftsPerRun)
                    {
                        break;
                    }

                    var existingReply = await replyRepository.GetReplyAsync(thread.ParentCommentId, cancellationToken);
                    // Skip if we've already pulled this
                    if (existingReply is not null)
                    {
                        continue;
                    }

                    string replyText;
                    if (IsEmojiOnly(thread.Text))
                    {
                        replyText = "🔥🙌";
                    }
                    else
                    {
                        var suggestion = await aiClient.SuggestReplyAsync(
                            video.Title ?? string.Empty,
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

                    await replyRepository.AddOrUpdateReplyAsync(reply, cancellationToken);

                    drafted++;
                }
            }
        }

        return drafted;
    }

    [GeneratedRegex(@"\p{L}|\p{N}")]
    private static partial Regex MyRegex();
}