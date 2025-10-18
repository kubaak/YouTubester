using System.ComponentModel;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using YouTubester.Integration.Dtos;

namespace YouTubester.Integration;

public sealed class YouTubeIntegration(YouTubeService youTubeService) : IYouTubeIntegration
{
    public async Task<string> GetMyChannelIdAsync(CancellationToken cancellationToken)
    {
        var chReq = youTubeService.Channels.List("id");
        chReq.Mine = true;
        var chRes = await chReq.ExecuteAsync(cancellationToken);
        return chRes.Items.First().Id!;
    }
    
    public async Task<string> GetUploadsPlaylistIdAsync(CancellationToken ct = default)
    {
        var chReq = youTubeService.Channels.List("contentDetails");
        chReq.Mine = true;
        var chRes = await chReq.ExecuteAsync(ct);
        var uploads = chRes.Items?.FirstOrDefault()?.ContentDetails?.RelatedPlaylists?.Uploads;
        if (string.IsNullOrWhiteSpace(uploads))
            throw new InvalidOperationException("Could not resolve uploads playlist for the authenticated channel.");
        return uploads!;
    }

    public async IAsyncEnumerable<VideoDto> GetAllVideosAsync(
        DateTimeOffset? publishedAfter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Resolve uploads playlist (mine=true)
        var chReq = youTubeService.Channels.List("contentDetails");
        chReq.Mine = true;
        var chRes = await chReq.ExecuteAsync(cancellationToken);
        var uploads = chRes.Items?.FirstOrDefault()?.ContentDetails?.RelatedPlaylists?.Uploads
                      ?? throw new InvalidOperationException("No uploads playlist for the authenticated channel.");

        string? page = null;
        var cutoffUtc = publishedAfter?.UtcDateTime;

        do
        {
            // Page newest → oldest with titles & descriptions
            var plReq = youTubeService.PlaylistItems.List("contentDetails,snippet");
            plReq.PlaylistId = uploads;
            plReq.MaxResults = 50;
            plReq.PageToken = page;

            var plRes = await plReq.ExecuteAsync(cancellationToken);
            var items = plRes.Items ?? new List<PlaylistItem>();
            if (items.Count == 0) yield break;

            // Early stop if whole page is older than cutoff
            if (cutoffUtc.HasValue)
            {
                var newest = items[0].ContentDetails?.VideoPublishedAt ?? items[0].Snippet?.PublishedAt;
                if (newest.HasValue && newest.Value <= cutoffUtc.Value) yield break;
            }

            // Collect ids + playlist metadata (title/desc/publishedAt)
            var pageIds = new List<string>(items.Count);
            var plMeta = new Dictionary<string, (string? Title, string? Description, DateTimeOffset? PublishedAt)>(StringComparer.Ordinal);

            foreach (var pi in items)
            {
                var id = pi.ContentDetails?.VideoId;
                if (string.IsNullOrWhiteSpace(id)) continue;

                var title = pi.Snippet?.Title;
                var description = pi.Snippet?.Description;
                var publishedAt = (DateTimeOffset?)pi.ContentDetails?.VideoPublishedAt ?? pi.Snippet?.PublishedAt;

                pageIds.Add(id);
                plMeta[id] = (title, description, publishedAt);
            }

            if (pageIds.Count == 0)
            {
                page = plRes.NextPageToken;
                continue;
            }

            // Fetch details needed for the domain factory
            // +snippet (channelId, title, tags, categoryId, defaultLanguage, defaultAudioLanguage)
            // +contentDetails (duration)
            // +status (privacy)
            // +recordingDetails (location & description)
            var vReq = youTubeService.Videos.List("snippet,contentDetails,status,recordingDetails");
            vReq.Id = string.Join(",", pageIds);
            var vRes = await vReq.ExecuteAsync(cancellationToken);
            var videos = vRes.Items ?? Enumerable.Empty<Google.Apis.YouTube.v3.Data.Video>();

            // Yield in playlist order (newest → older), honoring per-item cutoff
            foreach (var pi in items)
            {
                var id = pi.ContentDetails?.VideoId;
                if (string.IsNullOrWhiteSpace(id)) continue;

                var publishedAt = plMeta[id].PublishedAt;
                if (cutoffUtc.HasValue && publishedAt.HasValue && publishedAt.Value <= cutoffUtc.Value)
                    yield break;

                var v = videos.FirstOrDefault(x => x.Id == id);
                if (v is null) continue;

                var snippet = v.Snippet;
                var content = v.ContentDetails;
                var status  = v.Status;
                var rec     = v.RecordingDetails;

                var channelId = snippet?.ChannelId ?? ""; // you can also cache mine channel id once
                var title     = plMeta[id].Title ?? snippet?.Title ?? "(untitled)";
                var desc      = plMeta[id].Description ?? snippet?.Description ?? "";

                var tags = (snippet?.Tags ?? new List<string>())
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToArray();

                var iso = content?.Duration ?? "PT0S";
                var duration = System.Xml.XmlConvert.ToTimeSpan(iso);
                
                var isShort  = duration <= TimeSpan.FromSeconds(60);

                (double lat, double lng)? loc = null;
                var locObj = rec?.Location;
                if (locObj?.Latitude is double lat && locObj.Longitude is double lng)
                    loc = (lat, lng);

                yield return new VideoDto(
                    ChannelId: channelId,
                    VideoId: id,
                    Title: title,
                    Description: desc,
                    Tags: tags,
                    Duration: duration,
                    PrivacyStatus: status?.PrivacyStatus ?? "Private",
                    IsShort: isShort,
                    PublishedAt: publishedAt ?? DateTimeOffset.MinValue,
                    CategoryId: snippet?.CategoryId,
                    DefaultLanguage: snippet?.DefaultLanguage,
                    DefaultAudioLanguage: snippet?.DefaultAudioLanguage,
                    Location: loc,
                    LocationDescription: rec?.LocationDescription
                );
            }

            page = plRes.NextPageToken;
        }
        while (!string.IsNullOrEmpty(page));
    }
    
    public async IAsyncEnumerable<CommentThreadDto> GetUnansweredTopLevelCommentsAsync(
        string videoId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var myChannel = await GetMyChannelIdAsync(cancellationToken);

        string? page = null;
        do
        {
            var ctReq = youTubeService.CommentThreads.List("snippet,replies");
            ctReq.VideoId = videoId;
            ctReq.MaxResults = 50;
            ctReq.PageToken = page;
            ctReq.TextFormat = CommentThreadsResource.ListRequest.TextFormatEnum.PlainText;

            var ctRes = await ctReq.ExecuteAsync(cancellationToken);

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

    public async Task ReplyAsync(string parentCommentId, string text, CancellationToken cancellationToken)
    {
        var comment = new Comment
        {
            Snippet = new CommentSnippet
            {
                ParentId = parentCommentId,
                TextOriginal = text
            }
        };
        await youTubeService.Comments.Insert(comment, "snippet").ExecuteAsync(cancellationToken);
    }
    
    public async Task<VideoDetailsDto?> GetVideoDetailsAsync(string videoId, CancellationToken cancellationToken)
    {
        var req = youTubeService.Videos.List("snippet,recordingDetails");
        req.Id = videoId;
        var res = await req.ExecuteAsync(cancellationToken);
        var video = res.Items.FirstOrDefault();
        if (video is null) return null;

        var tags = video.Snippet?.Tags?.ToArray() ?? [];
        (double lat, double lng)? location = null;
        if (video.RecordingDetails?.Location is { } gp)
            location = (gp.Latitude ?? 0, gp.Longitude ?? 0);
        
        return new VideoDetailsDto(
            video.Id!,
            video.Snippet?.Title ?? "",
            video.Snippet?.Description ?? "",
            tags,
            video.Snippet?.CategoryId,
            video.Snippet?.DefaultLanguage,
            video.Snippet?.DefaultAudioLanguage,
            location,
            video.RecordingDetails?.LocationDescription
        );
    }
    
    public async Task UpdateVideoAsync(string videoId, string title, string description, IReadOnlyList<string> tags,
        string? categoryId, string? defaultLanguage, string? defaultAudioLanguage, 
        (double lat, double lng)? location, string? locationDescription, CancellationToken cancellationToken)
    {
        var snippet = new VideoSnippet
        {
            Title = title,
            Description = description,
            Tags = tags.ToList(),
            CategoryId = categoryId,
            DefaultLanguage = defaultLanguage,
            DefaultAudioLanguage = defaultAudioLanguage,
        };

        var video = new Video
        {
            Id = videoId,
            Snippet = snippet,
            RecordingDetails = new VideoRecordingDetails()
        };

        if (location is not null)
        {
            video.RecordingDetails.Location = new GeoPoint
            {
                Latitude = location.Value.lat,
                Longitude = location.Value.lng
            };
            video.RecordingDetails.LocationDescription = locationDescription;
        }

        var up = youTubeService.Videos.Update(video, "snippet,recordingDetails");
        await up.ExecuteAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<string>> GetPlaylistsContainingAsync(string videoId, CancellationToken cancellationToken)
    {
        var result = new List<string>();

        // list my playlists
        string? playListsPage = null;
        do
        {
            var playListRequest = youTubeService.Playlists.List("id");
            playListRequest.Mine = true;
            playListRequest.MaxResults = 50;
            playListRequest.PageToken = playListsPage;
            var plRes = await playListRequest.ExecuteAsync(cancellationToken);

            foreach (var playlist in plRes.Items)
            {
                // scan items of this playlist
                string? videosPage = null;
                do
                {
                    var itemsReq = youTubeService.PlaylistItems.List("contentDetails");
                    itemsReq.PlaylistId = playlist.Id;
                    itemsReq.MaxResults = 50;
                    itemsReq.PageToken = videosPage;
                    var itemsRes = await itemsReq.ExecuteAsync(cancellationToken);

                    if (itemsRes.Items.Any(i => i.ContentDetails?.VideoId == videoId))
                    {
                        result.Add(playlist.Id!);
                        break;
                    }
                    videosPage = itemsRes.NextPageToken;
                }
                while (videosPage is not null);
            }

            playListsPage = plRes.NextPageToken;
        }
        while (playListsPage is not null);

        return result;
    }
    
    public async Task AddVideoToPlaylistAsync(string playlistId, string videoId, CancellationToken cancellationToken)
    {
        // Check if already present to avoid duplicates
        string? page = null;
        do
        {
            var list = youTubeService.PlaylistItems.List("contentDetails");
            list.PlaylistId = playlistId;
            list.MaxResults = 50;
            list.PageToken = page;
            var res = await list.ExecuteAsync(cancellationToken);

            if (res.Items.Any(i => i.ContentDetails?.VideoId == videoId))
                return; // already there

            page = res.NextPageToken;
        }
        while (page is not null);

        var insert = youTubeService.PlaylistItems.Insert(new PlaylistItem
        {
            Snippet = new PlaylistItemSnippet
            {
                PlaylistId = playlistId,
                ResourceId = new ResourceId
                {
                    Kind = "youtube#video",
                    VideoId = videoId
                }
            }
        }, "snippet");

        await insert.ExecuteAsync(cancellationToken);
    }
}