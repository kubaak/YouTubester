using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Options;
using YouTubester.Integration.Configuration;

namespace YouTubester.Integration;

public sealed class YouTubeClientFactory(IOptions<YouTubeOptions> opt) : IYouTubeClientFactory
{
    private readonly YouTubeOptions _opt = opt.Value;

    public async Task<YouTubeService> CreateAsync(CancellationToken ct = default)
    {
        var cred = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets { ClientId = _opt.ClientId, ClientSecret = _opt.ClientSecret },
            new[] { YouTubeService.Scope.YoutubeForceSsl },
            "user",
            ct,
            new FileDataStore("YouTubeAuth", true)
        );

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = cred,
            ApplicationName = "YouTubester"
        });
    }
}