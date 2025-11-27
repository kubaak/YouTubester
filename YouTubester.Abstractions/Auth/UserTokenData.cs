namespace YouTubester.Abstractions.Auth;

public sealed class UserTokenData
{
    public string UserId { get; init; } = default!;
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}