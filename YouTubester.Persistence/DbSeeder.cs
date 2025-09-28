using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;

namespace YouTubester.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(YouTubesterDb db, CancellationToken ct = default)
    {
        if (!await db.Drafts.AnyAsync(ct))
        {
            db.Drafts.AddRange(
                new Reply {
                    CommentId = "demo-1",
                    VideoId = "demo-video-1",
                    VideoTitle = " Compilation",
                    CommentText = "This is amazing!",
                    Suggested = "Thanks! Glad you enjoyed it",
                    Approved = false
                },
                new Reply {
                    CommentId = "demo-2",
                    VideoId = "demo-video-2",
                    VideoTitle = "Awesome video",
                    CommentText = "❤❤❤❤❤❤😊🎉",
                    Suggested = "🔥 Appreciate the love! 🙌",
                    Approved = false
                }
            );

            await db.SaveChangesAsync(ct);
        }
    }
}