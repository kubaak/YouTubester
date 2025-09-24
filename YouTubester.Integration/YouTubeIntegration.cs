using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using YouTubester.Integration.Dtos;

namespace YouTubester.Integration;

public sealed class YouTubeIntegration(YouTubeService yt) : IYouTubeIntegration
{
    public async Task<string> GetMyChannelIdAsync(CancellationToken ct = default)
    {
        var chReq = yt.Channels.List("id");
        chReq.Mine = true;
        var chRes = await chReq.ExecuteAsync(ct);
        return chRes.Items.First().Id!;
    }

    public async IAsyncEnumerable<string> GetAllPublicVideoIdsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Find uploads playlist
        var chReq = yt.Channels.List("contentDetails");
        chReq.Mine = true;
        var chRes = await chReq.ExecuteAsync(ct);
        var uploads = chRes.Items.First().ContentDetails.RelatedPlaylists.Uploads;

        string? page = null;
        do
        {
            var plReq = yt.PlaylistItems.List("contentDetails");
            plReq.PlaylistId = uploads;
            plReq.MaxResults = 50;
            plReq.PageToken = page;

            var plRes = await plReq.ExecuteAsync(ct);
            var ids = string.Join(",", plRes.Items.Select(i => i.ContentDetails.VideoId));

            var vReq = yt.Videos.List("status");
            vReq.Id = ids;
            var vRes = await vReq.ExecuteAsync(ct);

            foreach (var v in vRes.Items.Where(v => v.Status?.PrivacyStatus == "public"))
                yield return v.Id;

            page = plRes.NextPageToken;
        } while (page != null);
    }

    public async Task<VideoDto?> GetVideoAsync(string videoId, CancellationToken ct = default)
    {
        var vReq = yt.Videos.List("snippet,contentDetails,status");
        vReq.Id = videoId;
        var vRes = await vReq.ExecuteAsync(ct);
        var v = vRes.Items.FirstOrDefault();
        if (v is null) return null;

        var duration = System.Xml.XmlConvert.ToTimeSpan(v.ContentDetails?.Duration ?? "PT0S");
        var tags = v.Snippet?.Tags?.ToArray() ?? Array.Empty<string>();
        var isShort = duration.TotalSeconds <= 60; // basic heuristic

        return new VideoDto(
            v.Id,
            v.Snippet?.Title ?? "",
            tags,
            duration,
            v.Status?.PrivacyStatus == "public",
            isShort
        );
    }

    public async IAsyncEnumerable<CommentThreadDto> GetUnansweredTopLevelCommentsAsync(
        string videoId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var myChannel = await GetMyChannelIdAsync(ct);

        string? page = null;
        do
        {
            var ctReq = yt.CommentThreads.List("snippet,replies");
            ctReq.VideoId = videoId;
            ctReq.MaxResults = 50;
            ctReq.PageToken = page;
            ctReq.TextFormat = CommentThreadsResource.ListRequest.TextFormatEnum.PlainText;

            var ctRes = await ctReq.ExecuteAsync(ct);

            foreach (var t in ctRes.Items)
            {
                var top = t.Snippet?.TopLevelComment;
                if (top is null) continue;

                var author = top.Snippet?.AuthorChannelId?.Value ?? "";
                // skip our own comments
                if (!string.IsNullOrEmpty(author) && author == myChannel) continue;

                // already answered by us?
                var anyOwnerReply = (t.Replies?.Comments ?? new List<Comment>()).Any(r =>
                    r.Snippet?.AuthorChannelId?.Value == myChannel);
                if (anyOwnerReply) continue;

                yield return new CommentThreadDto(
                    ParentCommentId: top.Id!,
                    VideoId: videoId,
                    AuthorChannelId: author,
                    Text: top.Snippet?.TextDisplay ?? ""
                );
            }

            page = ctRes.NextPageToken;
        } while (page != null);
    }

    public async Task ReplyAsync(string parentCommentId, string text, CancellationToken ct = default)
    {
        // YouTube caps comment length; trim to be safe
        var final = (text ?? "").Trim();
        if (final.Length > 320) final = final[..320];

        var comment = new Comment
        {
            Snippet = new CommentSnippet
            {
                ParentId = parentCommentId,
                TextOriginal = final
            }
        };
        await yt.Comments.Insert(comment, "snippet").ExecuteAsync(ct);
    }
}