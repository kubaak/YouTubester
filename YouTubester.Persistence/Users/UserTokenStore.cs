using Microsoft.EntityFrameworkCore;
using YouTubester.Domain;

namespace YouTubester.Persistence.Users;

public sealed class UserTokenStore(YouTubesterDb databaseContext) : IUserTokenStore
{
    public async Task UpsertGoogleTokenAsync(
        string userId,
        string? accessToken,
        string? refreshToken,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id must be a non-empty string.", nameof(userId));
        }

        var userTokens = await databaseContext.UserTokens
            .FirstOrDefaultAsync(entity => entity.UserId == userId, cancellationToken);

        if (userTokens is null)
        {
            userTokens = UserTokens.Create(userId, refreshToken, accessToken, expiresAt);
            await databaseContext.UserTokens.AddAsync(userTokens, cancellationToken);
        }
        else
        {
            userTokens.UpdateTokens(refreshToken, accessToken, expiresAt);
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserTokens?> GetGoogleTokensAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id must be a non-empty string.", nameof(userId));
        }

        var userTokens = await databaseContext.UserTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.UserId == userId, cancellationToken);

        return userTokens;
    }
}