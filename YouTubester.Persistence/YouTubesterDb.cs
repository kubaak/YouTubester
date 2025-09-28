using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;

namespace YouTubester.Persistence;

public class YouTubesterDb(DbContextOptions<YouTubesterDb> options) : DbContext(options)
{
    public DbSet<Reply> Drafts => Set<Reply>();
    public DbSet<PostedReply> PostedReplies => Set<PostedReply>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Reply>().HasKey(x => x.CommentId);
        b.Entity<Reply>().HasIndex(x => x.VideoId);
        //.HasConversion because Sqlite doesn't support DateTimeOffset
        b.Entity<Reply>().Property(x => x.CreatedAt).HasConversion(
            v => v.UtcDateTime,
            v => new DateTimeOffset(v, TimeSpan.Zero));
        b.Entity<Reply>().Property(x => x.PostedAt).HasConversion(
            v => !v.HasValue ? (DateTime?)null : v.Value.UtcDateTime, 
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);

        b.Entity<PostedReply>().HasKey(x => x.CommentId);
        b.Entity<PostedReply>().Property(x => x.PostedAt).HasConversion(
            v => v.UtcDateTime, v => new DateTimeOffset(v, TimeSpan.Zero));
    }
}
