namespace YouTubester.Domain;

public sealed class UserTokens
{
    public string UserId { get; private set; } = null!;
    public string? RefreshToken { get; private set; }
    public string? AccessToken { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public static UserTokens Create(string userId, string? refreshToken, string? accessToken,
        DateTimeOffset? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id must be a non-empty string.", nameof(userId));
        }

        var userTokens = new UserTokens
        {
            UserId = userId,
            RefreshToken = refreshToken,
            AccessToken = accessToken,
            ExpiresAt = expiresAt
        };

        return userTokens;
    }

    public void UpdateTokens(string? refreshToken, string? accessToken, DateTimeOffset? expiresAt)
    {
        RefreshToken = refreshToken;
        AccessToken = accessToken;
        ExpiresAt = expiresAt;
    }

    private UserTokens()
    {
    }
}