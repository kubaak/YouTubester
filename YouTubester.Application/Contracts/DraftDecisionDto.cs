namespace YouTubester.Application.Contracts;

public sealed record DraftDecisionDto(
    string CommentId,
    string? NewText,        // optional: amend the draft text
    bool Approve            // set true to mark as approved
);