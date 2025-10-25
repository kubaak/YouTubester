using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YouTubester.Integration.Configuration;

namespace YouTubester.Integration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYoutubeServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<YouTubeAuthOptions>(configuration.GetSection("YouTubeAuth"));
        services.AddSingleton<YouTubeService>(sp =>
        {
            var factory = sp.GetRequiredService<IYouTubeServiceFactory>();
            return factory.CreateAsync(CancellationToken.None).GetAwaiter().GetResult();
        });
        services.AddSingleton<IYouTubeServiceFactory, YouTubeServiceFactory>();
        services.AddScoped<IYouTubeIntegration, YouTubeIntegration>();
        return services;
    }

    public static IServiceCollection AddAiClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection("AI"));
        services.AddHttpClient<IAiClient, AiClient>((sp, http) =>
            {
                var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
                http.BaseAddress = new Uri(ai.Endpoint);
            })
            .AddStandardResilienceHandler();

        return services;
    }
}