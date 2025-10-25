using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using YouTubester.Api;
using YouTubester.Integration;
using YouTubester.Persistence;

namespace YouTubester.IntegrationTests.TestHost;

public class ApiTestWebAppFactory : WebApplicationFactory<Program>
{
    public string TestDatabasePath { get; }
    public CapturingBackgroundJobClient CapturingJobClient { get; }
    public Mock<IAiClient> MockAiClient { get; }
    public Mock<IYouTubeIntegration> MockYouTubeIntegration { get; }

    public ApiTestWebAppFactory()
    {
        TestDatabasePath = Path.Combine(
            Path.GetTempPath(),
            "YouTubester.IntegrationTests",
            "integration-test.db");

        var testDir = Path.GetDirectoryName(TestDatabasePath)!;
        Directory.CreateDirectory(testDir);

        CapturingJobClient = new CapturingBackgroundJobClient();
        MockAiClient = new Mock<IAiClient>(MockBehavior.Strict);
        MockYouTubeIntegration = new Mock<IYouTubeIntegration>(MockBehavior.Strict);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Remove app registrations we’re replacing
            services.RemoveAll<DbContextOptions<YouTubesterDb>>();
            services.RemoveAll<YouTubesterDb>();
            services.RemoveAll<IBackgroundJobClient>();
            services.RemoveAll<IAiClient>();
            services.RemoveAll<IYouTubeIntegration>();

            // Nuke ALL hosted services (covers Hangfire server and any BackgroundService)
            services.RemoveAll<IHostedService>();

            // Add test database
            services.AddDbContext<YouTubesterDb>(options =>
            {
                options.UseSqlite($"Data Source={TestDatabasePath}");
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });

            services.AddSingleton<IBackgroundJobClient>(CapturingJobClient);
            services.AddSingleton(MockAiClient.Object);
            services.AddSingleton(MockYouTubeIntegration.Object);
        });

        // Reduce logging noise in tests
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (File.Exists(TestDatabasePath))
            {
                try
                {
                    File.Delete(TestDatabasePath);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        base.Dispose(disposing);
    }
}