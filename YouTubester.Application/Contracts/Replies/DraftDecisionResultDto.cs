namespace YouTubester.Application.Contracts.Replies;

public sealed record DraftDecisionResultDto(
    string CommentId,
    bool Success,
    string? Error = null
);