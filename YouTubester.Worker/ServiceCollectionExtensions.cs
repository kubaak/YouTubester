using Hangfire;
using YouTubester.Application;
using YouTubester.Application.Jobs;
using YouTubester.Integration;
using YouTubester.Persistence;
using YouTubester.Persistence.Channels;
using YouTubester.Persistence.Playlists;
using YouTubester.Persistence.Replies;
using YouTubester.Persistence.Videos;

namespace YouTubester.Worker;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the worker’s core services. Flags let tests opt out of hosted services / server.
    /// </summary>
    public static IServiceCollection AddWorkerCore(
        this IServiceCollection services,
        IConfiguration config,
        string contentRootPath,
        bool addHostedServices = true,
        bool addHangfireServer = true)
    {
        services.Configure<WorkerOptions>(config.GetSection("Worker"));

        // DB (prod uses builder.Services.AddDatabase(rootPath))
        services.AddDatabase(contentRootPath);

        // External integrations
        services.AddYoutubeServices(config);
        services.AddAiClient(config);

        // Repositories
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<IPlaylistRepository, PlaylistRepository>();
        services.AddScoped<IReplyRepository, ReplyRepository>();

        // App services & jobs
        services.AddScoped<IVideoTemplatingService, VideoTemplatingService>();
        services.AddScoped<PostApprovedRepliesJob>();
        services.AddScoped<CopyVideoTemplateJob>();

        // Hangfire storage (no server yet)
        services.AddHangFireStorage(config, contentRootPath);

        if (addHostedServices)
        {
            services.AddHostedService<CommentScanWorker>();
        }

        if (addHangfireServer)
        {
            services.AddHangfireServer(o => o.Queues = ["replies", "templating", "default"]);
        }

        return services;
    }
}