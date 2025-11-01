using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using YouTubester.Integration.Dtos;

namespace YouTubester.Integration;

public sealed class YouTubeIntegration(YouTubeService youTubeService, ILogger<YouTubeIntegration> logger)
    : IYouTubeIntegration
{
    public async Task<ChannelDto?> GetChannelAsync(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return null;
        }

        try
        {
            // 1) Find the channelId by name using Search (most reliable if caller provides a human name)
            var search = youTubeService.Search.List("snippet");
            search.Q = channelName;
            search.Type = "channel";
            search.MaxResults = 1;

            var searchRes = await search.ExecuteAsync();
            var first = searchRes.Items?.FirstOrDefault();
            var channelId = first?.Snippet?.ChannelId;

            if (string.IsNullOrWhiteSpace(channelId))
            {
                logger.LogWarning("No channel found for name '{ChannelName}'", channelName);
                return null;
            }

            // 2) Fetch channel details to get uploads playlist + title + ETag
            var chReq = youTubeService.Channels.List("snippet,contentDetails");
            chReq.Id = channelId;

            var chRes = await chReq.ExecuteAsync();
            var ch = chRes.Items?.FirstOrDefault();
            if (ch is null)
            {
                logger.LogWarning("Channel id '{ChannelId}' not found when fetching details", channelId);
                return null;
            }

            var uploads = ch.ContentDetails?.RelatedPlaylists?.Uploads;
            if (string.IsNullOrWhiteSpace(uploads))
            {
                logger.LogWarning("Channel '{ChannelId}' has no uploads playlist", channelId);
                return null;
            }

            var title = ch.Snippet?.Title ?? channelName;
            var etag = ch.ETag;

            return new ChannelDto(
                channelId,
                title,
                uploads,
                etag
            );
        }
        catch (Google.GoogleApiException ex)
        {
            logger.LogError(ex, "YouTube API error while getting channel for '{ChannelName}'", channelName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while getting channel for '{ChannelName}'", channelName);
            return null;
        }
    }


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
                    location, video.RecordingDetails?.LocationDescription,
                    video.ETag, null // CommentsAllowed will be determined separately
                );
            }

            page = playlistResponse.NextPageToken;
        } while (!string.IsNullOrEmpty(page));
    }

    public async Task<bool?> CheckCommentsAllowedAsync(string videoId, CancellationToken cancellationToken)
    {
        try
        {
            // First check if video is made for kids (short-circuit)
            var videoRequest = youTubeService.Videos.List("status");
            videoRequest.Id = videoId;
            var videoResponse = await videoRequest.ExecuteAsync(cancellationToken);
            var video = videoResponse.Items?.FirstOrDefault();

            if (video?.Status != null)
            {
                if (video.Status.MadeForKids == true || video.Status.SelfDeclaredMadeForKids == true)
                {
                    return false; // Comments disabled for kids content
                }
            }

            // Check comments by attempting to list them
            var commentsRequest = youTubeService.CommentThreads.List("id");
            commentsRequest.VideoId = videoId;
            commentsRequest.MaxResults = 1;

            await commentsRequest.ExecuteAsync(cancellationToken);
            return true; // Comments allowed if request succeeds
        }
        catch (Google.GoogleApiException ex)
        {
            // Check if the error is specifically about comments being disabled
            if (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden &&
                ex.Error?.Errors?.Any(e => e.Reason == "commentsDisabled") == true)
            {
                return false;
            }

            logger.LogWarning(ex, "GoogleApiException Checking comments allowed for video {VideoId}", videoId);
            // For other errors, return null to indicate unknown status
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Checking comments allowed for video {VideoId}", videoId);
            // For any other exceptions, return null to indicate unknown status
            return null;
        }
    }

    public async Task<IReadOnlyList<VideoDto>> GetVideosAsync(IEnumerable<string> videoIds,
        CancellationToken cancellationToken)
    {
        var videoIdsList = videoIds.ToList();
        if (videoIdsList.Count == 0)
        {
            return Array.Empty<VideoDto>();
        }

        var videoRequest = youTubeService.Videos.List("snippet,contentDetails,status,recordingDetails");
        videoRequest.Id = string.Join(",", videoIdsList);

        var videoResponse = await videoRequest.ExecuteAsync(cancellationToken);
        var result = new List<VideoDto>();

        foreach (var video in videoResponse.Items)
        {
            if (string.IsNullOrWhiteSpace(video.Id))
            {
                continue;
            }

            var title = video.Snippet?.Title ?? string.Empty;
            var description = video.Snippet?.Description ?? string.Empty;
            var tags = video.Snippet?.Tags?.Where(t => !string.IsNullOrWhiteSpace(t));
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

            result.Add(new VideoDto(
                video.Id, title, description, tags, duration, privacyStatus,
                duration <= TimeSpan.FromSeconds(60),
                video.Snippet?.PublishedAtDateTimeOffset ?? DateTimeOffset.MinValue,
                video.Snippet?.CategoryId, video.Snippet?.DefaultLanguage, video.Snippet?.DefaultAudioLanguage,
                location, video.RecordingDetails?.LocationDescription,
                video.ETag, null // CommentsAllowed determined during comment scanning
            ));
        }

        return result;
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
                Latitude = location.Value.lat,
                Longitude = location.Value.lng
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
        //todo avoid checking to save the calls?
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

    public async IAsyncEnumerable<PlaylistDto> GetPlaylistsAsync(
        string channelId,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        string? page = null;
        do
        {
            var playlistRequest = youTubeService.Playlists.List("id,snippet");
            playlistRequest.ChannelId = channelId;
            playlistRequest.MaxResults = 50;
            playlistRequest.PageToken = page;

            var playlistResponse = await playlistRequest.ExecuteAsync(cancellationToken);

            if (playlistResponse.Items is null || playlistResponse.Items.Count == 0)
            {
                yield break;
            }

            foreach (var playlist in playlistResponse.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrWhiteSpace(playlist.Id))
                {
                    yield return new PlaylistDto(playlist.Id!, playlist.Snippet.Title, playlist.Snippet.ETag);
                }
            }

            page = playlistResponse.NextPageToken;
        } while (!string.IsNullOrEmpty(page));
    }

    public async IAsyncEnumerable<string> GetPlaylistVideoIdsAsync(
        string playlistId,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        string? page = null;
        do
        {
            var itemsRequest = youTubeService.PlaylistItems.List("contentDetails");
            itemsRequest.PlaylistId = playlistId;
            itemsRequest.MaxResults = 50;
            itemsRequest.PageToken = page;

            var itemsResponse = await itemsRequest.ExecuteAsync(cancellationToken);

            if (itemsResponse.Items is null || itemsResponse.Items.Count == 0)
            {
                yield break;
            }

            foreach (var item in itemsResponse.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var videoId = item.ContentDetails?.VideoId;
                if (!string.IsNullOrWhiteSpace(videoId))
                {
                    yield return videoId;
                }
            }

            page = itemsResponse.NextPageToken;
        } while (!string.IsNullOrEmpty(page));
    }

    public async IAsyncEnumerable<string> GetVideoIdsNewerThanAsync(
        string channelId,
        DateTimeOffset? cutoff,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        // Get the uploads playlist ID for the channel
        var channelRequest = youTubeService.Channels.List("contentDetails");
        channelRequest.Id = channelId;

        var channelResponse = await channelRequest.ExecuteAsync(cancellationToken);
        if (channelResponse.Items is null || channelResponse.Items.Count == 0)
        {
            yield break;
        }

        var uploadsPlaylistId = channelResponse.Items[0].ContentDetails?.RelatedPlaylists?.Uploads;
        if (string.IsNullOrWhiteSpace(uploadsPlaylistId))
        {
            yield break;
        }

        // Enumerate uploads playlist items
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

            // Early stop per page if cutoff is specified
            if (cutoff.HasValue)
            {
                var firstItem = playlistResponse.Items.First();
                var newest = firstItem.ContentDetails?.VideoPublishedAtDateTimeOffset ??
                             firstItem.Snippet?.PublishedAtDateTimeOffset;
                if (newest.HasValue && newest.Value <= cutoff.Value)
                {
                    yield break;
                }
            }

            foreach (var item in playlistResponse.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var videoId = item.ContentDetails?.VideoId;
                if (string.IsNullOrWhiteSpace(videoId))
                {
                    continue;
                }

                // Check cutoff within page
                if (cutoff.HasValue)
                {
                    var publishedAt = item.ContentDetails?.VideoPublishedAtDateTimeOffset ??
                                      item.Snippet?.PublishedAtDateTimeOffset;
                    if (publishedAt.HasValue && publishedAt.Value <= cutoff.Value)
                    {
                        yield break;
                    }
                }

                yield return videoId;
            }

            page = playlistResponse.NextPageToken;
        } while (!string.IsNullOrEmpty(page));
    }
}