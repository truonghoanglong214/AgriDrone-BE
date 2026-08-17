using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.PasswordResetTokens;

public sealed class PasswordResetToken : Entity
{
    private PasswordResetToken()
    {
    }

    private PasswordResetToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public User User { get; private set; } = null!;

    public static PasswordResetToken Create(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAt),
                expiresAt,
                "Expiration time must be later than creation time.");
        }

        return new PasswordResetToken(
            Guid.NewGuid(),
            userId,
            tokenHash,
            expiresAt,
            createdAt);
    }

    public bool CanBeUsed(DateTimeOffset now) =>
        UsedAt is null && RevokedAt is null && now < ExpiresAt;
}
