using YouTubester.Integration;
using YouTubester.Persistence;

namespace YouTubester.Application.Jobs;

public sealed class PostApprovedCommentsJob(IReplyRepository repo, IYouTubeIntegration yt)
{
    public async Task RunOne(string commentId, CancellationToken cancellationToken = default)
    {
        var draft = await repo.GetReplyAsync(commentId, cancellationToken);
        if (draft == null)
        {
            throw new ArgumentException($"The draft {commentId} could not be found.");
        }

        //todo transaction
        await yt.ReplyAsync(commentId, draft.FinalText!, cancellationToken);
        draft.Post(DateTimeOffset.Now);
        await repo.AddOrUpdateReplyAsync(draft, cancellationToken);
    }
}