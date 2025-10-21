using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;
using Channel = YouTubester.Domain.Channel;

namespace YouTubester.Persistence;

public class YouTubesterDb(DbContextOptions<YouTubesterDb> options) : DbContext(options)
{
    public DbSet<Reply> Replies => Set<Reply>();
    public DbSet<Channel> Channels => Set<Channel>();
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

        b.Entity<Channel>().HasKey(x => x.ChannelId);

        b.Entity<Video>().HasKey(v => new { v.UploadsPlaylistId, v.VideoId });
        b.Entity<Video>().HasIndex(x => x.UpdatedAt);
        // Composite index for video listing performance (PublishedAt DESC, VideoId DESC)
        b.Entity<Video>().HasIndex(v => new { v.PublishedAt, v.VideoId });
        //.HasConversion because Sqlite doesn't support DateTimeOffset
        b.Entity<Video>().Property(x => x.CachedAt).HasConversion(
            v => v.UtcDateTime,
            v => new DateTimeOffset(v, TimeSpan.Zero));
        b.Entity<Video>().Property(x => x.UpdatedAt).HasConversion(
            v => v.UtcDateTime,
            v => new DateTimeOffset(v, TimeSpan.Zero));
        b.Entity<Video>().Property(x => x.PublishedAt).HasConversion(
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