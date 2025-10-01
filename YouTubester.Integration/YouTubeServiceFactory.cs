using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Options;
using YouTubester.Integration.Configuration;

namespace YouTubester.Integration;

public interface IYouTubeServiceFactory
{
    Task<YouTubeService> CreateAsync(CancellationToken cancellationToken);
}

public sealed class YouTubeServiceFactory(IOptions<YouTubeAuthOptions> options) : IYouTubeServiceFactory
{
    public async Task<YouTubeService> CreateAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets { ClientId = o.ClientId, ClientSecret = o.ClientSecret },
            [YouTubeService.Scope.YoutubeForceSsl],
            o.UserName,
            cancellationToken,
            new FileDataStore(o.TokenStoreFolder, true)
        );

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credentials,
            ApplicationName = o.ApplicationName
        });
    }
}