namespace YouTubester.Application.Contracts;

public sealed record DraftDecisionResultDto(
    string CommentId,
    bool Success,
    string? Error = null
);