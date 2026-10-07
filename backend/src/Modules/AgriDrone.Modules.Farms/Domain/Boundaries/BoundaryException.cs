using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Domain.Boundaries;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "BoundaryException is the accepted business aggregate name in ADR-0008.")]
public sealed class BoundaryException : AggregateRoot
{
    private BoundaryException() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid FarmBoundaryVersionId { get; private set; }
    public BoundaryExceptionSource Source { get; private set; }
    public string SourceReferenceId { get; private set; } = null!;
    public Guid? SurveyOrderId { get; private set; }
    public Guid? MissionId { get; private set; }
    public Point OriginalPosition { get; private set; } = null!;
    public BoundaryExceptionState State { get; private set; }
    public decimal MeasuredDistanceMeters { get; private set; }
    public decimal ThresholdMeters { get; private set; }
    public string PolicyVersion { get; private set; } = null!;
    public BoundaryExceptionDecision? Decision { get; private set; }
    public Point? CorrectedPosition { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewReason { get; private set; }
    public JsonDocument? ReviewEvidence { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static BoundaryException Create(
        Guid tenantId,
        Guid farmId,
        Guid farmBoundaryVersionId,
        BoundaryExceptionSource source,
        string sourceReferenceId,
        Guid? surveyOrderId,
        Guid? missionId,
        Point originalPosition,
        BoundaryExceptionState initialState,
        decimal measuredDistanceMeters,
        decimal thresholdMeters,
        string policyVersion,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(farmBoundaryVersionId);
        DomainGuard.Utc(createdAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReferenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyVersion);

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        if (initialState is not BoundaryExceptionState.OutOfBoundary and
            not BoundaryExceptionState.NeedsReview)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialState),
                "A new boundary exception must be unresolved.");
        }

        if (surveyOrderId == Guid.Empty)
        {
            throw new ArgumentException("Survey order identifier cannot be empty.", nameof(surveyOrderId));
        }

        if (missionId == Guid.Empty)
        {
            throw new ArgumentException("Mission identifier cannot be empty.", nameof(missionId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(measuredDistanceMeters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thresholdMeters);

        var normalizedReference = sourceReferenceId.Trim();
        var normalizedPolicyVersion = policyVersion.Trim();
        if (normalizedReference.Length > 200)
        {
            throw new ArgumentException("Source reference cannot exceed 200 characters.", nameof(sourceReferenceId));
        }

        if (normalizedPolicyVersion.Length > 100)
        {
            throw new ArgumentException("Policy version cannot exceed 100 characters.", nameof(policyVersion));
        }

        return new BoundaryException
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FarmId = farmId,
            FarmBoundaryVersionId = farmBoundaryVersionId,
            Source = source,
            SourceReferenceId = normalizedReference,
            SurveyOrderId = surveyOrderId,
            MissionId = missionId,
            OriginalPosition = FarmBoundaryGeometryGuard.ValidPoint(originalPosition, nameof(originalPosition)),
            State = initialState,
            MeasuredDistanceMeters = measuredDistanceMeters,
            ThresholdMeters = thresholdMeters,
            PolicyVersion = normalizedPolicyVersion,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void Resolve(
        BoundaryExceptionDecision decision,
        Point? correctedPosition,
        Guid reviewedBy,
        DateTimeOffset reviewedAt,
        string reason,
        JsonDocument evidence)
    {
        if (State == BoundaryExceptionState.Resolved)
        {
            throw new FarmBoundaryDomainException(
                FarmBoundaryDomainErrorCodes.InvalidTransition,
                "A resolved boundary exception cannot be reviewed again.");
        }

        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        DomainGuard.NotEmpty(reviewedBy);
        DomainGuard.Utc(reviewedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(evidence);

        if (reviewedAt < CreatedAt)
        {
            throw new ArgumentException("Review time cannot be earlier than creation time.", nameof(reviewedAt));
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > 2000)
        {
            throw new ArgumentException("Review reason cannot exceed 2000 characters.", nameof(reason));
        }

        if (decision == BoundaryExceptionDecision.LocationCorrected && correctedPosition is null)
        {
            throw new FarmBoundaryDomainException(
                FarmBoundaryDomainErrorCodes.CorrectedPositionRequired,
                "LocationCorrected requires a corrected position.");
        }

        if (decision != BoundaryExceptionDecision.LocationCorrected && correctedPosition is not null)
        {
            throw new FarmBoundaryDomainException(
                FarmBoundaryDomainErrorCodes.CorrectedPositionNotAllowed,
                "Only LocationCorrected may store a corrected position.");
        }

        Decision = decision;
        CorrectedPosition = correctedPosition is null
            ? null
            : FarmBoundaryGeometryGuard.ValidPoint(correctedPosition, nameof(correctedPosition));
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewReason = normalizedReason;
        ReviewEvidence = JsonDocument.Parse(evidence.RootElement.GetRawText());
        State = BoundaryExceptionState.Resolved;
        UpdatedAt = reviewedAt;
        Version++;
    }

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new FarmBoundaryDomainException(
                FarmBoundaryDomainErrorCodes.VersionConflict,
                $"Expected BoundaryException version {expectedVersion}, but found {Version}.");
        }
    }
}
