using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using YouTubester.Application;
using YouTubester.Application.Jobs;
using YouTubester.Integration;
using YouTubester.Persistence;
using YouTubester.Persistence.Channels;
using YouTubester.Persistence.Replies;
using YouTubester.Persistence.Videos;
using YouTubester.Worker;

namespace YouTubester.IntegrationTests.TestHost;

public class WorkerTestHostFactory : IDisposable
{
    public IHost TestHost { get; }
    public string TestDatabasePath { get; }
    public CapturingBackgroundJobClient CapturingJobClient { get; }
    public Mock<IAiClient> MockAiClient { get; }
    public Mock<IYouTubeIntegration> MockYouTubeIntegration { get; }

    public WorkerTestHostFactory(string testDatabasePath)
    {
        TestDatabasePath = testDatabasePath;
        CapturingJobClient = new CapturingBackgroundJobClient();
        MockAiClient = new Mock<IAiClient>();
        MockYouTubeIntegration = new Mock<IYouTubeIntegration>();

        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(Array.Empty<string>());

        hostBuilder.UseEnvironment("Test");
        hostBuilder.ConfigureAppConfiguration(config =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Worker:CommentScanIntervalMinutes", "60"},
                {"ConnectionStrings:DefaultConnection", $"Data Source={TestDatabasePath}"},
            });
        });

        hostBuilder.ConfigureServices((context, services) =>
        {
            ConfigureServices(services, context.Configuration);
        });

        hostBuilder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        TestHost = hostBuilder.Build();
    }

    private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add Worker options
        services.Configure<WorkerOptions>(configuration.GetSection("Worker"));

        // Add test database
        services.AddDbContext<YouTubesterDb>(options =>
        {
            options.UseSqlite($"Data Source={TestDatabasePath}");
            options.EnableSensitiveDataLogging();
        });

        // Add repositories
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<IReplyRepository, ReplyRepository>();

        // Add services
        services.AddScoped<IVideoTemplatingService, VideoTemplatingService>();

        // Add jobs
        services.AddScoped<PostApprovedRepliesJob>();
        services.AddScoped<CopyVideoTemplateJob>();

        // Add test doubles
        services.AddSingleton<IBackgroundJobClient>(CapturingJobClient);
        services.AddSingleton(MockAiClient.Object);
        services.AddSingleton(MockYouTubeIntegration.Object);

        // Add Hangfire without server - using SQLite storage like the main app
        services.AddHangfire(config =>
        {
            config.UseSQLiteStorage(TestDatabasePath); 
        });

        // DON'T add Hangfire server or CommentScanWorker hosted service
        // We want to test jobs in isolation without background processing
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = TestHost.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public T GetRequiredService<T>() where T : notnull
    {
        return TestHost.Services.GetRequiredService<T>();
    }

    public void Dispose()
    {
        TestHost?.Dispose();
    }
}