using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace YouTubester.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, string projectFolder)
    {
        // EF DbContext (SQLite or Postgres)
        var dbPath = GetDbPath(projectFolder);

        services.AddDbContext<YouTubesterDb>(opt =>
            opt.UseSqlite($"Data Source={Path.GetFullPath(dbPath)}"));
        return services;
    }

    public static string GetDbPath(string projectFolder)
    {
        var solutionRoot = Directory.GetParent(projectFolder)!.FullName; // up one
        var dataDir = Path.Combine(solutionRoot, ".data");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "youtubester.db");
        return dbPath;
    }
}