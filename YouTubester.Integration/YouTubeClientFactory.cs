using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Options;
using YouTubester.Integration.Configuration;

namespace YouTubester.Integration;

public sealed class YouTubeClientFactory(IOptions<YouTubeOptions> options) : IYouTubeClientFactory
{
    private readonly YouTubeOptions _opt = options.Value;

    public async Task<YouTubeService> CreateAsync(CancellationToken cancellationToken)
    {
        var cred = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets { ClientId = _opt.ClientId, ClientSecret = _opt.ClientSecret },
            new[] { YouTubeService.Scope.YoutubeForceSsl },
            "user",
            cancellationToken,
            new FileDataStore("YouTubeAuth", true)
        );

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = cred,
            ApplicationName = "YouTubester"
        });
    }
}