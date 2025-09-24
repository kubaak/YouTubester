using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace YouTubester;

public static class Comments
{
    public static async IAsyncEnumerable<CommentThread> GetUnansweredThreadsAsync(
        YouTubeService yt, string videoId, string myChannelId)
    {
        string? page = null;
        do
        {
            var tReq = yt.CommentThreads.List("snippet,replies");
            tReq.VideoId = videoId;
            tReq.MaxResults = 100;
            tReq.TextFormat = CommentThreadsResource.ListRequest.TextFormatEnum.PlainText;
            tReq.PageToken = page;
            var tRes = await tReq.ExecuteAsync();

            foreach (var th in tRes.Items ?? Enumerable.Empty<CommentThread>())
            {
                var repliedInIncluded = th.Replies?.Comments?.Any(c =>
                    c.Snippet?.AuthorChannelId?.Value == myChannelId) == true;

                if (repliedInIncluded) continue;

                // If zero replies -> unanswered
                if (th.Snippet?.TotalReplyCount == 0)
                {
                    yield return th;
                    continue;
                }

                // There are replies; check all for my reply
                var parentId = th.Snippet?.TopLevelComment?.Id;
                if (string.IsNullOrEmpty(parentId)) continue;

                var mineFound = false;
                string? cp = null;
                do
                {
                    var cReq = yt.Comments.List("snippet");
                    cReq.ParentId = parentId;
                    cReq.MaxResults = 100;
                    cReq.TextFormat = CommentsResource.ListRequest.TextFormatEnum.PlainText;
                    cReq.PageToken = cp;
                    var cRes = await cReq.ExecuteAsync();

                    if (cRes.Items?.Any(c => c.Snippet?.AuthorChannelId?.Value == myChannelId) == true)
                    {
                        mineFound = true;
                        break;
                    }
                    cp = cRes.NextPageToken;
                } while (cp != null);

                if (!mineFound) yield return th;
            }

            page = tRes.NextPageToken;
        } while (page != null);
    }

    public static async Task PostReplyAsync(YouTubeService yt, string parentCommentId, string text)
    {
        var comment = new Comment
        {
            Snippet = new CommentSnippet { ParentId = parentCommentId, TextOriginal = text }
        };
        await yt.Comments.Insert(comment, "snippet").ExecuteAsync();
    }
}