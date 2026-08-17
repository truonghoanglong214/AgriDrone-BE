using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories;

internal sealed class PasswordResetTokenRepository(IdentityDbContext context)
    : IPasswordResetTokenRepository
{
    public void Add(PasswordResetToken passwordResetToken) =>
        context.PasswordResetTokens.Add(passwordResetToken);

    public Task<PasswordResetToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        context.PasswordResetTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);

    public async Task RevokeActiveForUserAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        await context.PasswordResetTokens
            .Where(token =>
                token.UserId == userId &&
                token.UsedAt == null &&
                token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    token => token.RevokedAt,
                    revokedAt),
                cancellationToken);
    }

    public async Task<bool> TryMarkUsedAsync(
        Guid tokenId,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await context.PasswordResetTokens
            .Where(token =>
                token.Id == tokenId &&
                token.UsedAt == null &&
                token.RevokedAt == null &&
                token.ExpiresAt > usedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    token => token.UsedAt,
                    usedAt),
                cancellationToken);

        return affectedRows == 1;
    }
}
