using Google.Apis.YouTube.v3;

namespace YouTubester;

public static class Channel
{
    public static async Task<string?> GetMyChannelIdAsync(YouTubeService yt)
    {
        var req = yt.Channels.List("id,contentDetails");
        req.Mine = true;
        var res = await req.ExecuteAsync();
        return res.Items.FirstOrDefault()?.Id;
    }

    public record VideoInfo(string Id, bool IsShort, string Title, string[] Tags);

    public static async IAsyncEnumerable<VideoInfo> GetAllPublicVideoIdsAsync(YouTubeService yt)
    {
        // Get uploads playlist id
        var chReq = yt.Channels.List("contentDetails");
        chReq.Mine = true;
        var chRes = await chReq.ExecuteAsync();
        var uploads = chRes.Items.First().ContentDetails.RelatedPlaylists.Uploads;

        // Page through uploads playlist items
        string? page = null;
        do
        {
            var plReq = yt.PlaylistItems.List("contentDetails");
            plReq.PlaylistId = uploads;
            plReq.MaxResults = 50;
            plReq.PageToken = page;
            var plRes = await plReq.ExecuteAsync();

            // Batch video status for privacy filter
            var ids = string.Join(",", plRes.Items.Select(i => i.ContentDetails.VideoId));
            var vReq = yt.Videos.List("status,contentDetails,snippet");
            vReq.Id = ids;
            var vRes = await vReq.ExecuteAsync();
            

            foreach (var v in vRes.Items.Where(v => v.Status?.PrivacyStatus == "public"))
            {
                var durIso = v.ContentDetails?.Duration ?? "PT0S";
                var ts = System.Xml.XmlConvert.ToTimeSpan(durIso);
                var isShort = ts.TotalSeconds <= 60;

                var title = v.Snippet?.Title ?? "";
                var tags = v.Snippet?.Tags?.ToArray() ?? [];

                yield return new VideoInfo(v.Id, isShort, title, tags);
            }

            page = plRes.NextPageToken;
        } while (page != null);
    }

}