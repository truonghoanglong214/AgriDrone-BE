namespace AgriDrone.Modules.Identity.Domain.PasswordResetTokens;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task RevokeActiveForUserAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default);

    Task<bool> TryMarkUsedAsync(
        Guid tokenId,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken = default);

    void Add(PasswordResetToken passwordResetToken);
}
