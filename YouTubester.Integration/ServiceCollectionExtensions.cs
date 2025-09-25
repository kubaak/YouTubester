using Google.Apis.YouTube.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace YouTubester.Integration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYoutubeServices(this IServiceCollection services)
    {
        services.AddSingleton<YouTubeService>(sp =>
        {
            var factory = sp.GetRequiredService<IYouTubeServiceFactory>();
            return factory.CreateAsync(CancellationToken.None).GetAwaiter().GetResult();
        });
        services.AddSingleton<IYouTubeServiceFactory, YouTubeServiceFactory>();
        services.AddScoped<IYouTubeIntegration, YouTubeIntegration>();
        return services;
    }

    public static IServiceCollection AddAiClient(this IServiceCollection services)
    {
        services.AddHttpClient<IAiClient, AiClient>((sp, http) =>
        {
            var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            http.BaseAddress = new Uri(ai.Endpoint);
        });
        return services;
    }
}