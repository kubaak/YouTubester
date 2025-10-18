using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;

namespace YouTubester.Persistence;

public class YouTubesterDb(DbContextOptions<YouTubesterDb> options) : DbContext(options)
{
    public DbSet<Reply> Replies => Set<Reply>();
    public DbSet<Video> Videos => Set<Video>();

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

        b.Entity<Video>().HasKey(x => x.VideoId);//todo composite key
        b.Entity<Video>().HasIndex(x => x.UpdatedAt);
        //.HasConversion because Sqlite doesn't support DateTimeOffset
        b.Entity<Video>().Property(x => x.UpdatedAt).HasConversion(
            v => v.UtcDateTime,
            v => new DateTimeOffset(v, TimeSpan.Zero));
        b.Entity<Video>()
            .OwnsOne(v => v.Location, x =>
        {
            x.Property(p => p.Latitude);
            x.Property(p => p.Longitude);
            x.WithOwner();
        });
    }
}
