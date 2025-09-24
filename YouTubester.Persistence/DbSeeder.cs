using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;

namespace YouTubester.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(YouTubesterDb db, CancellationToken ct = default)
    {
        if (!await db.Drafts.AnyAsync(ct))
        {
            var now = DateTimeOffset.UtcNow;

            db.Drafts.AddRange(
                new ReplyDraft {
                    CommentId = "demo-1",
                    VideoId = "demo-video-1",
                    VideoTitle = " Compilation",
                    CommentText = "This is amazing!",
                    Suggested = "Thanks! Glad you enjoyed it",
                    Approved = false,
                    CreatedAt = now, UpdatedAt = now
                },
                new ReplyDraft {
                    CommentId = "demo-2",
                    VideoId = "demo-video-2",
                    VideoTitle = "Awesome video",
                    CommentText = "❤❤❤❤❤❤😊🎉",
                    Suggested = "🔥 Appreciate the love! 🙌",
                    Approved = false,
                    CreatedAt = now, UpdatedAt = now
                }
            );

            await db.SaveChangesAsync(ct);
        }
    }
}