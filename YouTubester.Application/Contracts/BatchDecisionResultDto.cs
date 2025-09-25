namespace YouTubester.Application.Contracts;

public sealed record BatchDecisionResultDto(
    int Total,
    int Succeeded,
    int Failed,
    IReadOnlyList<DraftDecisionResultDto> Items
);