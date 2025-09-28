using YouTubester.Integration;
using YouTubester.Persistence;

namespace YouTubester.Application.Jobs;

public sealed class PostApprovedCommentsJob(ICommentRepository repo, IYouTubeIntegration yt)
{
    public async Task RunOne(string commentId, CancellationToken ct = default)
    {
        var draft = await repo.GetDraftAsync(commentId);
        if (draft == null)
        {
            throw new ArgumentException($"The draft {commentId} could not be found.");
        }

        //todo transaction
        await yt.ReplyAsync(commentId, draft.FinalText!, ct);
        draft.Post(DateTimeOffset.Now);
        await repo.AddOrUpdateDraftAsync(draft);
    }
}