using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;

namespace YouTubester.Persistence;

public class YouTubesterDb(DbContextOptions<YouTubesterDb> options) : DbContext(options)
{
    public DbSet<Reply> Replies => Set<Reply>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        //todo indexes
        b.Entity<Reply>().HasKey(x => x.CommentId);
        b.Entity<Reply>().HasIndex(x => x.VideoId);
        //.HasConversion because Sqlite doesn't support DateTimeOffset
        b.Entity<Reply>().Property(x => x.PulledAt).HasConversion(
            v => v.UtcDateTime,
            v => new DateTimeOffset(v, TimeSpan.Zero));
        b.Entity<Reply>().Property(x => x.PostedAt).HasConversion(
            v => !v.HasValue ? (DateTime?)null : v.Value.UtcDateTime, 
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);
    }
}
