namespace YouTubester.Abstractions.Auth;

public interface IUserTokenStore
{
    Task<UserTokenData?> GetAsync(string userId, CancellationToken cancellationToken);

    Task UpsertAsync(
        string userId,
        string? accessToken,
        string? refreshToken,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken);
}