using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace YouTubester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Authenticating...");

            var config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var updateVideos = config.GetValue<bool>("YouTube:UpdateVideos");
            var clientId = config["YouTube:ClientId"];
            var clientSecret = config["YouTube:ClientSecret"];
            var referenceUrl = config["YouTube:ReferenceVideoUrl"];
            var targetPlaylistNames = config.GetSection("YouTube:TargetPlaylists").Get<string[]>() ?? [];
            var aiEnable     = config.GetValue<bool>("YouTube:AI:Enable");
            var aiProvider   = config["YouTube:AI:Provider"];
            var aiEndpoint   = config["YouTube:AI:Endpoint"];
            var aiModel      = config["YouTube:AI:Model"];
            var replaceTitle = config.GetValue<bool>("YouTube:AI:ReplaceTitle");
            var applyDescription = config.GetValue<bool>("YouTube:AI:ApplyDescription");
            
            var referenceVideoId = ExtractVideoIdFromUrl(referenceUrl);
            if (string.IsNullOrWhiteSpace(referenceVideoId))
            {
                Console.WriteLine("Invalid reference video URL.");
                return;
            }

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                },
                [YouTubeService.Scope.YoutubeForceSsl],
                "user",
                CancellationToken.None,
                new FileDataStore("YouTubeAuth")
            );

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "YouTubester"
            });

            if (updateVideos)
            {
                await UpdateVideos(referenceVideoId, youtubeService, targetPlaylistNames, aiEnable, aiProvider,
                    aiEndpoint, aiModel, replaceTitle, applyDescription);
            }
            
            var replyToComments = config.GetValue<bool>("YouTube:AI:ReplyToComments");
            var maxReplies = Math.Max(0, config.GetValue<int>("YouTube:AI:MaxRepliesPerRun"));
            var dryRun = config.GetValue<bool>("YouTube:AI:DryRunReplies");
            var aiEnabled = config.GetValue<bool>("YouTube:AI:Enable");

            if (replyToComments && aiEnabled && string.Equals(aiProvider, "ollama", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyToComments(youtubeService, maxReplies, aiEndpoint, aiModel, dryRun);
            }
        }

        private static async Task UpdateVideos(string referenceVideoId, YouTubeService youtubeService,
            string[] targetPlaylistNames, bool aiEnable, string? aiProvider, string? aiEndpoint, string? aiModel,
            bool replaceTitle, bool applyDescription)
        {
            Console.WriteLine($"Fetching reference video ID: {referenceVideoId}");

            var referenceRequest = youtubeService.Videos.List("snippet,status,recordingDetails");
            referenceRequest.Id = referenceVideoId;
            var referenceResponse = await referenceRequest.ExecuteAsync();
            var referenceVideo = referenceResponse.Items.FirstOrDefault();
                
            if (referenceVideo == null)
            {
                Console.WriteLine("Reference video not found.");
                return;
            }

            var refSnippet = referenceVideo.Snippet;
            var refStatus = referenceVideo.Status;
            var refRec = referenceVideo.RecordingDetails;

            var referenceTags = refSnippet.Tags ?? Array.Empty<string>();

            // Get all playlists owned by the user (for name -> id mapping)
            var playlistListRequest = youtubeService.Playlists.List("snippet");
            playlistListRequest.Mine = true;
            playlistListRequest.MaxResults = 50;
            var playlistListResponse = await playlistListRequest.ExecuteAsync();

            var targetPlaylists = playlistListResponse.Items
                .Where(p => targetPlaylistNames.Contains(p.Snippet.Title, StringComparer.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine("Scanning for unlisted videos referencing the source video...");

            var searchRequest = youtubeService.Search.List("id");
            searchRequest.ForMine = true;
            searchRequest.Type = "video";
            searchRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
            searchRequest.MaxResults = 50;

            var searchResponse = await searchRequest.ExecuteAsync();

            foreach (var result in searchResponse.Items)
            {
                var videoId = result.Id.VideoId;
                var videoRequest = youtubeService.Videos.List("snippet,status,recordingDetails");
                videoRequest.Id = videoId;
                var videoResponse = await videoRequest.ExecuteAsync();
                var video = videoResponse.Items.FirstOrDefault();

                if (video == null || video.Status.PrivacyStatus != "unlisted")
                    continue;

                if (!video.Snippet.Description.Contains(referenceVideoId, StringComparison.OrdinalIgnoreCase) &&
                    !video.Snippet.Title.Contains(referenceVideoId, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Run once per short: if we've already added any of the reference tags, skip
                // if (video.Snippet.Tags != null && video.Snippet.Tags.Intersect(referenceTags).Any())
                // {
                //     Console.WriteLine($"Skipping video {videoId} — already tagged once.");
                //     continue;
                // }

                Console.WriteLine($"Matched unlisted video: {videoId}, updating metadata...");

                // --- Copy from reference ---
                // Tags
                video.Snippet.Tags = referenceTags;

                // Description: append hashtags only (do not replace existing)
                var hashtags = string.Join(" ", referenceTags.Select(tag => $"#{tag.Replace(" ", "")}"));
                if (!string.IsNullOrWhiteSpace(hashtags))
                {
                    video.Snippet.Description = (video.Snippet.Description ?? string.Empty) + $"\n\n{hashtags}";
                }

                // Languages & Category
                if (!string.IsNullOrEmpty(refSnippet.DefaultLanguage))
                    video.Snippet.DefaultLanguage = refSnippet.DefaultLanguage;
                if (!string.IsNullOrEmpty(refSnippet.DefaultAudioLanguage))
                    video.Snippet.DefaultAudioLanguage = refSnippet.DefaultAudioLanguage;
                if (!string.IsNullOrEmpty(refSnippet.CategoryId))
                    video.Snippet.CategoryId = refSnippet.CategoryId;

                // Status flags
                if (refStatus?.MadeForKids != null)
                    video.Status.MadeForKids = refStatus.MadeForKids;
                if (!string.IsNullOrEmpty(refStatus?.License))
                    video.Status.License = refStatus.License; // "youtube" or "creativeCommon"
                if (refStatus?.Embeddable != null)
                    video.Status.Embeddable = refStatus.Embeddable.Value;
                if (refStatus?.PublicStatsViewable != null)
                    video.Status.PublicStatsViewable = refStatus.PublicStatsViewable.Value;

                // Recording details
                if (refRec != null)
                {
                    video.RecordingDetails ??= new VideoRecordingDetails();
                    if (refRec.Location != null)
                        video.RecordingDetails.Location = refRec.Location;
                    if (!string.IsNullOrEmpty(refRec.LocationDescription))
                        video.RecordingDetails.LocationDescription = refRec.LocationDescription;
                    if (refRec.RecordingDate != null)
                        video.RecordingDetails.RecordingDate = refRec.RecordingDate;
                }
                    

                if (aiEnable && string.Equals(aiProvider, "ollama", StringComparison.OrdinalIgnoreCase))
                {
                    var (aiTitle, aiDesc) = await LocalAi.SuggestMetadataAsync(
                        aiEndpoint!, aiModel!,
                        video.Snippet.Title,
                        referenceVideo.Snippet.Description
                    );

                    if (!string.IsNullOrWhiteSpace(aiTitle) && replaceTitle)
                    {
                        video.Snippet.Title = aiTitle!.Length > 80 ? aiTitle[..80].Trim() : aiTitle.Trim();
                        Console.WriteLine($"Applied AI title: {video.Snippet.Title}");
                    }
                    else if (!string.IsNullOrWhiteSpace(aiTitle))
                    {
                        Console.WriteLine($"Suggested AI title (preview): {aiTitle}");
                    }

                    if (applyDescription && !string.IsNullOrWhiteSpace(aiDesc))
                    {
                        var cleanDesc = aiDesc.Trim();
                            
                        // Avoid double-adding if already present
                        if (!string.IsNullOrWhiteSpace(hashtags) &&
                            !cleanDesc.Contains(hashtags, StringComparison.OrdinalIgnoreCase))
                        {
                            cleanDesc += "\n\n" + hashtags;
                        }

                        video.Snippet.Description = cleanDesc;
                        Console.WriteLine("Applied AI description (+ hashtags).");
                    }
                }

                var updateRequest = youtubeService.Videos.Update(video, "snippet,status,recordingDetails");
                await updateRequest.ExecuteAsync();

                Console.WriteLine($"Updated video: {videoId}");

                // Add to target playlists by name (if configured)
                foreach (var playlist in targetPlaylists)
                {
                    var insertRequest = new PlaylistItem
                    {
                        Snippet = new PlaylistItemSnippet
                        {
                            PlaylistId = playlist.Id,
                            ResourceId = new ResourceId
                            {
                                Kind = "youtube#video",
                                VideoId = videoId
                            }
                        }
                    };

                    var playlistInsertRequest = youtubeService.PlaylistItems.Insert(insertRequest, "snippet");
                    await playlistInsertRequest.ExecuteAsync();

                    Console.WriteLine($"Added video {videoId} to playlist '{playlist.Snippet.Title}'");
                }
            }

            Console.WriteLine("Done Updating videos.");
        }

        private static async Task ReplyToComments(YouTubeService youtubeService, int maxReplies, string? aiEndpoint,
            string? aiModel, bool dryRun)
        {
            var myChannelId = await Channel.GetMyChannelIdAsync(youtubeService);
            if (string.IsNullOrEmpty(myChannelId))
            {
                Console.WriteLine("Could not determine my channel id; skipping replies.");
            }
            else
            {
                Console.WriteLine("Scanning all public videos for unanswered comments…");
                var replied = 0;

                await foreach (var videoInfo in Channel.GetAllPublicVideoIdsAsync(youtubeService))
                {
                    if (replied >= maxReplies) break;

                    // Get title + tags for context
                    var vReq = youtubeService.Videos.List("snippet");
                    vReq.Id = videoInfo.Id;
                    var vRes = await vReq.ExecuteAsync();
                    var v = vRes.Items.FirstOrDefault();
                    var title = v?.Snippet?.Title ?? "";
                    var tags = v?.Snippet?.Tags ?? Array.Empty<string>();

                    await foreach (var th in Comments.GetUnansweredThreadsAsync(youtubeService, videoInfo.Id,
                                       myChannelId))
                    {
                        if (replied >= maxReplies) break;

                        var top = th.Snippet?.TopLevelComment;
                        var parentId = top?.Id;
                        var text = top?.Snippet?.TextDisplay ?? top?.Snippet?.TextOriginal ?? "";
                        if (string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(text)) continue;

                        var isShort = videoInfo.IsShort;

                        string reply;
                        if (IsEmojiOnly(text))
                        {
                            reply = "😊";
                        }
                        else
                        {
                            reply = await LocalAi.SuggestReplyAsync(aiEndpoint!, aiModel!, title, tags, text, isShort);
                        }
                            
                        if (string.IsNullOrWhiteSpace(reply)) continue;

                        reply = reply.Trim();
                        if (reply.Length > 320) reply = reply[..320];

                        Console.WriteLine($"Title: {title} | Comment: {text}");
                        Console.WriteLine($"Reply: {reply}");

                        if (!dryRun)
                        {
                            try { await Comments.PostReplyAsync(youtubeService, parentId, reply); replied++; }
                            catch (Exception ex) { Console.WriteLine($"Failed to post: {ex.Message}"); }
                        }
                        else
                        {
                            replied++;
                        }
                    }
                }

                Console.WriteLine($"Reply pass complete. Posted (or staged) {replied} replies.");
            }
        }

        private static bool IsEmojiOnly(string text)
        {
            // If there's no letter or digit in the comment, assume it's emoji-only
            return !Regex.IsMatch(text, @"\p{L}|\p{N}");
        }

        private static string ExtractVideoIdFromUrl(string url)
        {
            var match = Regex.Match(url ?? "", @"(?:youtube\.com.*[?&]v=|youtu\.be/)([\w-]{11})");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
