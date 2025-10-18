namespace YouTubester.Application.Contracts.Replies;

public sealed record DraftDecisionDto(
    string CommentId,
    string ApprovedText
);