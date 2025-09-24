using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Options;
using YouTubester.Integration.Configuration;

namespace YouTubester.Integration;

public interface IYouTubeServiceFactory
{
    Task<YouTubeService> CreateAsync(CancellationToken ct = default);
}

public sealed class YouTubeServiceFactory(IOptions<YouTubeAuthOptions> opt) : IYouTubeServiceFactory
{
    public async Task<YouTubeService> CreateAsync(CancellationToken ct = default)
    {
        var o = opt.Value;
        var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets { ClientId = o.ClientId, ClientSecret = o.ClientSecret },
            [YouTubeService.Scope.YoutubeForceSsl],
            o.UserName,
            ct,
            new FileDataStore(o.TokenStoreFolder, true)
        );

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credentials,
            ApplicationName = o.ApplicationName
        });
    }
}