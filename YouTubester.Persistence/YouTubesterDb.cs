using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;

namespace YouTubester.Persistence;

public class YouTubesterDb(DbContextOptions<YouTubesterDb> options) : DbContext(options)
{
    public DbSet<ReplyDraft> Drafts => Set<ReplyDraft>();
    public DbSet<PostedReply> PostedReplies => Set<PostedReply>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ReplyDraft>().HasKey(x => x.CommentId);
        b.Entity<ReplyDraft>().HasIndex(x => x.VideoId);
        //.HasConversion because Sqlite doesn't support DateTimeOffset
        b.Entity<ReplyDraft>().Property(x => x.CreatedAt).HasConversion(
            v => v.UtcDateTime, v => new DateTimeOffset(v, TimeSpan.Zero));
        b.Entity<ReplyDraft>().Property(x => x.UpdatedAt).HasConversion(
            v => v.UtcDateTime, v => new DateTimeOffset(v, TimeSpan.Zero));

        b.Entity<PostedReply>().HasKey(x => x.CommentId);
        b.Entity<PostedReply>().Property(x => x.PostedAt).HasConversion(
            v => v.UtcDateTime, v => new DateTimeOffset(v, TimeSpan.Zero));
    }
}
