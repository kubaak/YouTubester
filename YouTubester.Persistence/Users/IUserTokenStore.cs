using YouTubester.Domain;

namespace YouTubester.Persistence.Users;

public interface IUserTokenStore
{
    Task UpsertGoogleTokenAsync(
        string userId,
        string? accessToken,
        string? refreshToken,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken);

    Task<UserToken?> GetGoogleTokenAsync(string userId, CancellationToken cancellationToken);
}