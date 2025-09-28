namespace YouTubester.Application.Contracts;

public sealed record DraftDecisionDto(
    string CommentId,
    string ApprovedText
);