using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
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
        MockAiClient = new Mock<IAiClient>();
        MockYouTubeIntegration = new Mock<IYouTubeIntegration>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        
        builder.ConfigureServices(services =>
        {
            // Remove existing DB context registration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<YouTubesterDb>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var dbContextServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(YouTubesterDb));
            if (dbContextServiceDescriptor != null)
            {
                services.Remove(dbContextServiceDescriptor);
            }

            // Add test database
            services.AddDbContext<YouTubesterDb>(options =>
            {
                options.UseSqlite($"Data Source={TestDatabasePath}");
                options.EnableSensitiveDataLogging();
            });

            // Replace Hangfire background job client with capturing one
            var jobClientDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IBackgroundJobClient));
            if (jobClientDescriptor != null)
            {
                services.Remove(jobClientDescriptor);
            }
            services.AddSingleton<IBackgroundJobClient>(CapturingJobClient);

            // Replace AI client with mock
            var aiClientDescriptors = services.Where(
                d => d.ServiceType == typeof(IAiClient)).ToList();
            foreach (var descriptor in aiClientDescriptors)
            {
                services.Remove(descriptor);
            }
            services.AddSingleton(MockAiClient.Object);

            // Replace YouTube integration with mock
            var youtubeDescriptors = services.Where(
                d => d.ServiceType == typeof(IYouTubeIntegration)).ToList();
            foreach (var descriptor in youtubeDescriptors)
            {
                services.Remove(descriptor);
            }
            services.AddSingleton(MockYouTubeIntegration.Object);

            // Remove Hangfire Server if registered
            var hangfireServerDescriptor = services.Where(
                d => d.ImplementationType?.Name.Contains("BackgroundJobServer") == true).ToList();
            foreach (var descriptor in hangfireServerDescriptor)
            {
                services.Remove(descriptor);
            }

            // Remove any hosted services that might start Hangfire server
            var hostedServices = services.Where(d => 
                typeof(IHostedService).IsAssignableFrom(d.ServiceType) ||
                typeof(IHostedService).IsAssignableFrom(d.ImplementationType)).ToList();
            
            foreach (var service in hostedServices)
            {
                if (service.ImplementationType?.Name.Contains("Hangfire") == true ||
                    service.ImplementationType?.Name.Contains("BackgroundJob") == true)
                {
                    services.Remove(service);
                }
            }
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
            // Clean up test database file
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