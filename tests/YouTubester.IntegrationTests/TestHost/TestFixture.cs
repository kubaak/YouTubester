using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using YouTubester.Persistence;

namespace YouTubester.IntegrationTests.TestHost;

public class TestFixture : IAsyncLifetime
{
    public ApiTestWebAppFactory ApiFactory { get; private set; } = null!;
    public WorkerTestHostFactory WorkerFactory { get; private set; } = null!;
    public HttpClient HttpClient { get; private set; } = null!;
    public IServiceProvider ApiServices => ApiFactory.Services;
    public IServiceProvider WorkerServices => WorkerFactory.TestHost.Services;

    public async Task InitializeAsync()
    {
        ApiFactory = new ApiTestWebAppFactory();
        WorkerFactory = new WorkerTestHostFactory(ApiFactory.TestDatabasePath);
        HttpClient = ApiFactory.CreateClient();

        // Ensure database is created once
        await ApiFactory.EnsureDatabaseCreatedAsync();
        await WorkerFactory.EnsureDatabaseCreatedAsync();
    }

    public async Task ResetDbAsync()
    {
        using var scope = ApiServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
        await SqliteCleaner.CleanAsync(dbContext.Database.GetDbConnection());
        
        // Clear capturing job client
        ApiFactory.CapturingJobClient.Clear();
        WorkerFactory.CapturingJobClient.Clear();
    }

    public Task DisposeAsync()
    {
        HttpClient?.Dispose();
        WorkerFactory?.Dispose();
        ApiFactory?.Dispose();
        return Task.CompletedTask;
    }
}