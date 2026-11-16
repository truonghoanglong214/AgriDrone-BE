using System.Security.Cryptography;
using System.Text;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SpatialPolicy : Entity
{
    private SpatialPolicy() { }

    public string StreamKey { get; private set; } = null!;
    public int VersionNumber { get; private set; }
    public Guid? PredecessorVersionId { get; private set; }
    public double BoundaryToleranceM { get; private set; }
    public double MatchingDistanceThresholdM { get; private set; }
    public double RowBearingToleranceDeg { get; private set; }
    public double GridSpacingToleranceM { get; private set; }
    public string PolicyFingerprint { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string ReasonCode { get; private set; } = null!;
    public string? EvidenceReference { get; private set; }

    public static SpatialPolicy Create(
        Guid id,
        string streamKey,
        int versionNumber,
        Guid? predecessorVersionId,
        double boundaryToleranceM,
        double matchingDistanceThresholdM,
        double rowBearingToleranceDeg,
        double gridSpacingToleranceM,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid createdBy,
        DateTimeOffset createdAt,
        string reasonCode,
        string? evidenceReference = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(createdBy, nameof(createdBy));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var normalizedStreamKey = PolicyValidationGuard.NormalizeRequired(streamKey, 100, nameof(streamKey));
        PolicyValidationGuard.ValidateVersionAndPredecessor(id, versionNumber, predecessorVersionId);
        PolicyValidationGuard.ValidateEffectiveWindow(effectiveFrom, effectiveTo);

        if (boundaryToleranceM <= 0 || boundaryToleranceM > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(boundaryToleranceM), "Boundary tolerance must be between 0 and 50 meters.");
        }

        if (matchingDistanceThresholdM <= 0 || matchingDistanceThresholdM > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(matchingDistanceThresholdM), "Matching distance threshold must be between 0 and 20 meters.");
        }

        if (rowBearingToleranceDeg <= 0 || rowBearingToleranceDeg > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(rowBearingToleranceDeg), "Row bearing tolerance must be between 0 and 180 degrees.");
        }

        if (gridSpacingToleranceM <= 0 || gridSpacingToleranceM > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(gridSpacingToleranceM), "Grid spacing tolerance must be between 0 and 10 meters.");
        }

        var normalizedReasonCode = PolicyValidationGuard.NormalizeRequired(reasonCode, 100, nameof(reasonCode));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));

        var fingerprintInput = $"{normalizedStreamKey}|{versionNumber}|{boundaryToleranceM:F2}|{matchingDistanceThresholdM:F2}|{rowBearingToleranceDeg:F1}|{gridSpacingToleranceM:F2}|{effectiveFrom:O}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput))).ToLowerInvariant();

        return new SpatialPolicy
        {
            Id = id,
            StreamKey = normalizedStreamKey,
            VersionNumber = versionNumber,
            PredecessorVersionId = predecessorVersionId,
            BoundaryToleranceM = boundaryToleranceM,
            MatchingDistanceThresholdM = matchingDistanceThresholdM,
            RowBearingToleranceDeg = rowBearingToleranceDeg,
            GridSpacingToleranceM = gridSpacingToleranceM,
            PolicyFingerprint = fingerprint,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ReasonCode = normalizedReasonCode,
            EvidenceReference = normalizedEvidence
        };
    }
}
