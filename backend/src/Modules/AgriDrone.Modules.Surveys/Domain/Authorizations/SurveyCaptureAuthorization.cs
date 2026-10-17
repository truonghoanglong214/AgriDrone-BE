using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

/// <summary>
/// Versioned evidence gate for data capture. Request approval creates pending
/// requirements; only an explicit evidence-bearing verification opens the gate.
/// </summary>
public sealed class SurveyCaptureAuthorization : Entity
{
    private SurveyCaptureAuthorization() { }

    public Guid SurveyOrderId { get; private set; }
    public SurveyCaptureAuthorizationType Type { get; private set; }
    public SurveyCaptureAuthorizationStatus Status { get; private set; }
    public string? EvidenceReference { get; private set; }
    public DateTimeOffset? IssuedAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public Guid? VerifiedBy { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public string? RevocationReason { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;

    public static SurveyCaptureAuthorization CreatePending(
        Guid surveyOrderId,
        SurveyCaptureAuthorizationType type,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.Utc(createdAt);
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        return new SurveyCaptureAuthorization
        {
            Id = Guid.CreateVersion7(),
            SurveyOrderId = surveyOrderId,
            Type = type,
            Status = SurveyCaptureAuthorizationStatus.Pending,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void Verify(
        string evidenceReference,
        DateTimeOffset issuedAt,
        DateTimeOffset? expiresAt,
        Guid verifiedBy,
        DateTimeOffset verifiedAt,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        DomainGuard.NotEmpty(verifiedBy);
        DomainGuard.Utc(issuedAt);
        DomainGuard.Utc(verifiedAt);
        if (expiresAt.HasValue)
        {
            DomainGuard.Utc(expiresAt.Value);
            if (expiresAt <= verifiedAt)
            {
                throw new ArgumentException(
                    "Authorization expiry must be after verification.",
                    nameof(expiresAt));
            }
        }

        if (Status == SurveyCaptureAuthorizationStatus.Revoked)
        {
            throw new InvalidOperationException(
                "A revoked authorization cannot be verified again.");
        }

        EvidenceReference = Normalize(evidenceReference, 1000);
        IssuedAt = issuedAt;
        VerifiedAt = verifiedAt;
        ExpiresAt = expiresAt;
        VerifiedBy = verifiedBy;
        Status = SurveyCaptureAuthorizationStatus.Verified;
        UpdatedAt = verifiedAt;
        Version++;
    }

    public void Revoke(
        string reason,
        Guid revokedBy,
        DateTimeOffset revokedAt,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        DomainGuard.NotEmpty(revokedBy);
        DomainGuard.Utc(revokedAt);
        if (Status != SurveyCaptureAuthorizationStatus.Verified)
        {
            throw new InvalidOperationException(
                "Only a verified authorization can be revoked.");
        }

        RevocationReason = Normalize(reason, 2000);
        RevokedBy = revokedBy;
        RevokedAt = revokedAt;
        Status = SurveyCaptureAuthorizationStatus.Revoked;
        UpdatedAt = revokedAt;
        Version++;
    }

    public bool IsValidAt(DateTimeOffset evaluatedAt) =>
        Status == SurveyCaptureAuthorizationStatus.Verified &&
        VerifiedAt <= evaluatedAt &&
        (!ExpiresAt.HasValue || evaluatedAt < ExpiresAt.Value) &&
        !RevokedAt.HasValue;

    private void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException(
                $"Authorization version conflict: expected {expectedVersion}, current {Version}.");
        }
    }

    private static string Normalize(string value, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            normalized.Length,
            maximumLength,
            nameof(value));

        return normalized;
    }
}
