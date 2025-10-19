using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace YouTubester.Persistence;

public sealed class YouTubesterDbFactory : IDesignTimeDbContextFactory<YouTubesterDb>
{
    public YouTubesterDb CreateDbContext(string[] args)
    {
        var persistenceDir = Directory.GetCurrentDirectory();
        var dbPath = ServiceCollectionExtensions.GetDbPath(persistenceDir);
        var options = new DbContextOptionsBuilder<YouTubesterDb>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new YouTubesterDb(options);
    }
}