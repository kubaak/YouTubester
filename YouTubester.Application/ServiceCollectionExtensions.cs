using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YouTubester.Application.Options;

namespace YouTubester.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVideoListingOptions(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<VideoListingOptions>(configuration.GetSection("VideoListing"));
        services.AddOptions<VideoListingOptions>()
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }

    public static IServiceCollection AddReplyListingOptions(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ReplyListingOptions>(configuration.GetSection("ReplyListing"));
        services.AddOptions<ReplyListingOptions>()
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }
}
