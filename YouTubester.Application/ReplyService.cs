using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YouTubester.Application.Common;
using YouTubester.Application.Contracts;
using YouTubester.Application.Contracts.Replies;
using YouTubester.Application.Exceptions;
using YouTubester.Application.Jobs;
using YouTubester.Application.Options;
using YouTubester.Domain;
using YouTubester.Persistence.Replies;

namespace YouTubester.Application;

public class ReplyService(
    IReplyRepository repository,
    IBackgroundJobClient backgroundJobClient,
    IOptions<ReplyListingOptions> options,
    ILogger<ReplyService> logger)
    : IReplyService
{
    public Task<IEnumerable<Reply>> GetRepliesForApprovalAsync(CancellationToken cancellationToken)
    {
        return repository.GetRepliesForApprovalAsync(cancellationToken);
    }

    public async Task<Reply?> DeleteAsync(string commentId, CancellationToken cancellationToken)
    {
        return await repository.DeleteReplyAsync(commentId, cancellationToken);
    }

    public async Task<BatchIgnoreResult> IgnoreBatchAsync(string[] commentIds, CancellationToken ct)
    {
        var ids = commentIds.Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0)
        {
            return new BatchIgnoreResult(0, 0, 0, 0, 0, [], [], [], []);
        }

        var matches = await repository.LoadStatusesAsync(ids, ct);
        var matchedSet = matches.Select(m => m.CommentId).ToHashSet(StringComparer.Ordinal);

        var notFound = ids.Where(id => !matchedSet.Contains(id)).ToArray();
        var alreadyIgnored = matches.Where(m => m.Status == ReplyStatus.Ignored).Select(m => m.CommentId).ToArray();
        var skippedPosted = matches.Where(m => m.Status == ReplyStatus.Posted).Select(m => m.CommentId).ToArray();

        var toIgnore = matches
            .Where(m => m.Status != ReplyStatus.Posted && m.Status != ReplyStatus.Ignored)
            .Select(m => m.CommentId)
            .ToArray();

        var actuallyIgnored = toIgnore.Length == 0
            ? []
            : await repository.IgnoreManyAsync(toIgnore, ct);

        return new BatchIgnoreResult(
            ids.Length,
            actuallyIgnored.Length,
            alreadyIgnored.Length,
            skippedPosted.Length,
            notFound.Length,
            actuallyIgnored,
            alreadyIgnored,
            skippedPosted,
            notFound
        );
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
                    results.Add(new DraftDecisionResultDto(d.CommentId, false, "Draft not found"));
                    fail++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(d.ApprovedText))
                {
                    results.Add(new DraftDecisionResultDto(d.CommentId, false, "Draft is empty"));
                    fail++;
                    continue;
                }

                draft.ApproveText(d.ApprovedText, DateTimeOffset.Now);
                //todo from configuration
                backgroundJobClient.Schedule<PostApprovedRepliesJob>(
                    j => j.Run(draft.CommentId, JobCancellationToken.Null), TimeSpan.FromSeconds(10));

                await repository.AddOrUpdateReplyAsync(draft, cancellationToken);
                results.Add(new DraftDecisionResultDto(d.CommentId, true));
                ok++;
            }
            catch (Exception ex)
            {
                results.Add(new DraftDecisionResultDto(d.CommentId, false, ex.Message));
                fail++;
            }
        }

        return new BatchDecisionResultDto(
            ok + fail,
            ok,
            fail,
            results);
    }

    public async Task<PagedResult<ReplyListItemDto>> GetRepliesAsync(ReplyStatus[]? statuses, int? pageSize, string? pageToken, CancellationToken ct)
    {
        var opts = options.Value;
        var effectivePageSize = pageSize ?? opts.DefaultPageSize;
        if (effectivePageSize < 1 || effectivePageSize > opts.MaxPageSize)
        {
            throw new InvalidPageSizeException(effectivePageSize, opts.MaxPageSize);
        }

        var statusBinding = statuses is null ? string.Empty : string.Join(',', statuses.OrderBy(s => s));
        var binding = statusBinding;

        DateTimeOffset? afterPulledAtUtc = null;
        string? afterCommentId = null;
        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            if (!RepliesPageToken.TryParse(pageToken, out var pulledAt, out var commentId, out var tokenBinding))
            {
                logger.LogWarning("Invalid page token received");
                throw new InvalidPageTokenException();
            }

            if (!string.IsNullOrEmpty(tokenBinding) && !string.Equals(tokenBinding, binding, StringComparison.Ordinal))
            {
                logger.LogWarning("Page token binding mismatch");
                throw new InvalidPageTokenException("Page token does not match current filters.");
            }

            afterPulledAtUtc = pulledAt;
            afterCommentId = commentId;
        }

        // Fetch one extra item to determine if there's a next page
        var take = effectivePageSize + 1;
        var replies = await repository.GetRepliesPageAsync(statuses, afterPulledAtUtc, afterCommentId, take, ct);

        // Determine if there are more items
        var hasMore = replies.Count > effectivePageSize;
        var itemsToReturn = hasMore ? replies.Take(effectivePageSize).ToList() : replies;

        string? nextPageToken = null;
        if (hasMore && itemsToReturn.Count > 0)
        {
            var lastItem = itemsToReturn[^1];
            nextPageToken = RepliesPageToken.Serialize(lastItem.PulledAt, lastItem.CommentId, binding);
        }

        var items = itemsToReturn.Select(r => new ReplyListItemDto
        {
            CommentId = r.CommentId,
            VideoId = r.VideoId,
            VideoTitle = r.VideoTitle,
            CommentText = r.CommentText,
            Status = r.Status,
            PulledAt = r.PulledAt,
            SuggestedAt = r.SuggestedAt,
            ApprovedAt = r.ApprovedAt,
            PostedAt = r.PostedAt
        }).ToList();

        return new PagedResult<ReplyListItemDto> { Items = items, NextPageToken = nextPageToken };
    }
}
