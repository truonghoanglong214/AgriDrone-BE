using System.Text.RegularExpressions;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.RefreshSessions;

public sealed class RefreshSessionToken : Entity
{
    private static readonly Regex Hex64Regex = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    private RefreshSessionToken()
    {
    }

    public Guid SessionId { get; private set; }

    public int Generation { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public Guid? PredecessorTokenId { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public RefreshSession Session { get; private set; } = null!;

    public RefreshSessionToken? PredecessorToken { get; private set; }

    public bool IsUsed => UsedAt.HasValue;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public static RefreshSessionToken CreateInitial(
        Guid id,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt) => Create(
            id,
            sessionId,
            generation: 1,
            tokenHash,
            predecessorTokenId: null,
            issuedAt,
            expiresAt);

    public static RefreshSessionToken CreateRotated(
        RefreshSessionToken predecessor,
        Guid id,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (id == predecessor.Id)
        {
            throw new ArgumentException("Rotated token Id must differ from predecessor Id.", nameof(id));
        }

        return Create(
            id,
            predecessor.SessionId,
            checked(predecessor.Generation + 1),
            tokenHash,
            predecessor.Id,
            issuedAt,
            expiresAt);
    }

    private static RefreshSessionToken Create(
        Guid id,
        Guid sessionId,
        int generation,
        string tokenHash,
        Guid? predecessorTokenId,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(sessionId);
        DomainGuard.Utc(issuedAt);
        DomainGuard.Utc(expiresAt);

        if (generation < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(generation), "Generation must be >= 1.");
        }

        if (generation == 1 && predecessorTokenId.HasValue)
        {
            throw new ArgumentException("Generation 1 token cannot have a predecessor.", nameof(predecessorTokenId));
        }

        if (generation > 1 && (!predecessorTokenId.HasValue || predecessorTokenId.Value == Guid.Empty || predecessorTokenId.Value == id))
        {
            throw new ArgumentException("Generation > 1 token requires a valid distinct predecessor.", nameof(predecessorTokenId));
        }

        var normalizedHash = tokenHash?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Hex64Regex.IsMatch(normalizedHash))
        {
            throw new ArgumentException("TokenHash must be a 64-character lowercase SHA-256 hex digest.", nameof(tokenHash));
        }

        if (expiresAt <= issuedAt)
        {
            throw new ArgumentException("ExpiresAt must be greater than IssuedAt.", nameof(expiresAt));
        }

        return new RefreshSessionToken
        {
            Id = id,
            SessionId = sessionId,
            Generation = generation,
            TokenHash = normalizedHash,
            PredecessorTokenId = predecessorTokenId,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt
        };
    }

    public void MarkUsed(DateTimeOffset usedAt)
    {
        DomainGuard.Utc(usedAt);
        if (UsedAt.HasValue)
        {
            return;
        }

        UsedAt = usedAt;
    }
}
