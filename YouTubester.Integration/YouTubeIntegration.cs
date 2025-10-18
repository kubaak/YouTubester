using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using YouTubester.Integration.Dtos;

namespace YouTubester.Integration;

public sealed class YouTubeIntegration(YouTubeService youTubeService) : IYouTubeIntegration
{
    public async IAsyncEnumerable<VideoDto> GetAllVideosAsync(
        string uploadsPlaylistId,
        DateTimeOffset? publishedAfter,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        string? page = null;

        do
        {
            var playlistRequest = youTubeService.PlaylistItems.List("contentDetails,snippet");
            playlistRequest.PlaylistId = uploadsPlaylistId;
            playlistRequest.MaxResults = 50;
            playlistRequest.PageToken = page;

            var playlistResponse = await playlistRequest.ExecuteAsync(cancellationToken);
            if (playlistResponse.Items is null || playlistResponse.Items.Count == 0)
            {
                yield break;
            }

            if (publishedAfter.HasValue)
            {
                var firstItem = playlistResponse.Items.First();
                var newest = firstItem.ContentDetails?.VideoPublishedAtDateTimeOffset ??
                             firstItem.Snippet?.PublishedAtDateTimeOffset;
                if (newest.HasValue && newest.Value <= publishedAfter.Value)
                {
                    yield break;
                }
            }

            var videoIds = new List<string>(playlistResponse.Items.Count);
            var metaDictionary =
                new Dictionary<string, (string? Title, string? Description, DateTimeOffset? PublishedAt)>(StringComparer
                    .Ordinal);

            foreach (var item in playlistResponse.Items)
            {
                var videoId = item.ContentDetails?.VideoId;
                if (string.IsNullOrWhiteSpace(videoId))
                {
                    continue;
                }

                videoIds.Add(videoId);
                metaDictionary[videoId] = (
                    item.Snippet?.Title,
                    item.Snippet?.Description,
                    item.Snippet?.PublishedAtDateTimeOffset ?? item.ContentDetails?.VideoPublishedAtDateTimeOffset
                );
            }

            if (videoIds.Count == 0)
            {
                page = playlistResponse.NextPageToken;
                continue;
            }

            var videoListRequest = youTubeService.Videos.List("snippet,contentDetails,status,recordingDetails");
            videoListRequest.Id = string.Join(",", videoIds);
            var videoResponse = await videoListRequest.ExecuteAsync(cancellationToken);

            foreach (var video in videoResponse.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(video.Id))
                {
                    continue;
                }

                var (metaTitle, metaDescription, metaPublishedAt) = metaDictionary[video.Id];
                if (publishedAfter.HasValue && metaPublishedAt.HasValue &&
                    metaPublishedAt.Value <= publishedAfter.Value)
                {
                    yield break;
                }

                var title = metaTitle ?? video.Snippet.Title ?? string.Empty;
                var description = metaDescription ?? video.Snippet.Title ?? string.Empty;

                var tags = video.Snippet?.Tags?
                    .Where(t => !string.IsNullOrWhiteSpace(t));

                var iso = video.ContentDetails?.Duration ?? "PT0S";
                var duration = System.Xml.XmlConvert.ToTimeSpan(iso);

                var privacy = video.Status?.PrivacyStatus ?? "private";
                var publishAt = video.Status?.PublishAtDateTimeOffset;
                var isScheduled = string.Equals(privacy, "private", StringComparison.OrdinalIgnoreCase)
                                  && publishAt.HasValue
                                  && publishAt.Value > DateTimeOffset.UtcNow;

                var privacyStatus = isScheduled ? "scheduled" : privacy;

                var geoPoint = video.RecordingDetails?.Location;
                var latitude = geoPoint?.Latitude;
                var longitude = geoPoint?.Longitude;
                ValueTuple<double, double>? location = null;
                if (latitude is not null && longitude is not null)
                {
                    location = new ValueTuple<double, double>(latitude.Value, longitude.Value);
                }

                yield return new VideoDto(
                    video.Id, title, description, tags, duration, privacyStatus,
                    duration <= TimeSpan.FromSeconds(60), metaPublishedAt ?? DateTimeOffset.MinValue,
                    video.Snippet?.CategoryId, video.Snippet?.DefaultLanguage, video.Snippet?.DefaultAudioLanguage,
                    location, video.RecordingDetails?.LocationDescription
                );
            }

            page = playlistResponse.NextPageToken;
        } while (!string.IsNullOrEmpty(page));
    }

    public async IAsyncEnumerable<CommentThreadDto> GetUnansweredTopLevelCommentsAsync(
        string channelId,
        string videoId,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
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
                if (top is null)
                {
                    continue;
                }

                var author = top.Snippet?.AuthorChannelId?.Value ?? "";
                // skip our own comments
                if (!string.IsNullOrEmpty(author) && author == channelId)
                {
                    continue;
                }

                // already answered by us?
                var anyOwnerReply = (t.Replies?.Comments ?? new List<Comment>()).Any(r =>
                    r.Snippet?.AuthorChannelId?.Value == channelId);
                if (anyOwnerReply)
                {
                    continue;
                }

                yield return new CommentThreadDto(
                    top.Id!,
                    videoId,
                    author,
                    top.Snippet?.TextDisplay ?? ""
                );
            }

            page = ctRes.NextPageToken;
        } while (page != null);
    }

    public async Task ReplyAsync(string parentCommentId, string text, CancellationToken cancellationToken)
    {
        var comment = new Comment { Snippet = new CommentSnippet { ParentId = parentCommentId, TextOriginal = text } };
        await youTubeService.Comments.Insert(comment, "snippet").ExecuteAsync(cancellationToken);
    }

    public async Task<VideoDetailsDto?> GetVideoDetailsAsync(string videoId, CancellationToken cancellationToken)
    {
        var req = youTubeService.Videos.List("snippet,recordingDetails");
        req.Id = videoId;
        var res = await req.ExecuteAsync(cancellationToken);
        var video = res.Items.FirstOrDefault();
        if (video is null)
        {
            return null;
        }

        var tags = video.Snippet?.Tags?.ToArray() ?? [];
        (double lat, double lng)? location = null;
        if (video.RecordingDetails?.Location is { } gp)
        {
            location = (gp.Latitude ?? 0, gp.Longitude ?? 0);
        }

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
            DefaultAudioLanguage = defaultAudioLanguage
        };

        var video = new Video { Id = videoId, Snippet = snippet, RecordingDetails = new VideoRecordingDetails() };

        if (location is not null)
        {
            video.RecordingDetails.Location = new GeoPoint
            {
                Latitude = location.Value.lat, Longitude = location.Value.lng
            };
            video.RecordingDetails.LocationDescription = locationDescription;
        }

        var up = youTubeService.Videos.Update(video, "snippet,recordingDetails");
        await up.ExecuteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetPlaylistsContainingAsync(string videoId,
        CancellationToken cancellationToken)
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
                } while (videosPage is not null);
            }

            playListsPage = plRes.NextPageToken;
        } while (playListsPage is not null);

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
            {
                return; // already there
            }

            page = res.NextPageToken;
        } while (page is not null);

        var insert = youTubeService.PlaylistItems.Insert(
            new PlaylistItem
            {
                Snippet = new PlaylistItemSnippet
                {
                    PlaylistId = playlistId,
                    ResourceId = new ResourceId { Kind = "youtube#video", VideoId = videoId }
                }
            }, "snippet");

        await insert.ExecuteAsync(cancellationToken);
    }
}