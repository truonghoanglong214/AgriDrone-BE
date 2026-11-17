using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Farms.Domain.Maps.Staging;

public sealed class MappingCandidateBatch : AggregateRoot
{
    private readonly List<MappingCandidate> _candidates = [];

    private MappingCandidateBatch() { }

    public MappingKind MappingKind { get; private set; }
    public Guid HandoffId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid MissionId { get; private set; }
    public Guid FarmBoundaryVersionId { get; private set; }
    public Guid? FarmBaseMapVersionId { get; private set; }
    public Guid? ExpectedCurrentFarmBaseMapVersionId { get; private set; }
    public double GridBearingDeg { get; private set; }
    public double RowSpacingM { get; private set; }
    public double PlantSpacingM { get; private set; }
    public Guid JobId { get; private set; }
    public Guid ModelVersionId { get; private set; }
    public string ModelVersion { get; private set; } = string.Empty;
    public string AlgorithmVersion { get; private set; } = string.Empty;
    public string BoundaryPolicyVersion { get; private set; } = string.Empty;
    public DateTimeOffset ProducedAt { get; private set; }
    public Guid SpatialPolicyVersionId { get; private set; }
    public string SpatialPolicyFingerprint { get; private set; } = string.Empty;
    public double BoundaryToleranceM { get; private set; }
    public double MatchingDistanceThresholdM { get; private set; }
    public double RowBearingToleranceDeg { get; private set; }
    public double GridSpacingToleranceM { get; private set; }
    public MappingCandidateBatchStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewReason { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<MappingCandidate> Candidates => _candidates.AsReadOnly();

    public static MappingCandidateBatch Create(
        Guid id,
        MappingKind mappingKind,
        Guid handoffId,
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        Guid missionId,
        Guid farmBoundaryVersionId,
        Guid? farmBaseMapVersionId,
        Guid? expectedCurrentFarmBaseMapVersionId,
        double gridBearingDeg,
        double rowSpacingM,
        double plantSpacingM,
        Guid jobId,
        Guid modelVersionId,
        string modelVersion,
        string algorithmVersion,
        string boundaryPolicyVersion,
        DateTimeOffset producedAt,
        Guid spatialPolicyVersionId,
        string spatialPolicyFingerprint,
        double boundaryToleranceM,
        double matchingDistanceThresholdM,
        double rowBearingToleranceDeg,
        double gridSpacingToleranceM,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(handoffId);
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(missionId);
        DomainGuard.NotEmpty(farmBoundaryVersionId);
        DomainGuard.NotEmpty(jobId);
        DomainGuard.NotEmpty(modelVersionId);
        DomainGuard.NotEmpty(spatialPolicyVersionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(spatialPolicyFingerprint);
        DomainGuard.Utc(producedAt);
        DomainGuard.Utc(createdAt);
        ArgumentOutOfRangeException.ThrowIfNegative(gridBearingDeg);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(gridBearingDeg, 360);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowSpacingM);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(plantSpacingM);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(boundaryToleranceM);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(matchingDistanceThresholdM);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowBearingToleranceDeg);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gridSpacingToleranceM);

        return new MappingCandidateBatch
        {
            Id = id,
            MappingKind = mappingKind,
            HandoffId = handoffId,
            TenantId = tenantId,
            FarmId = farmId,
            SurveyOrderId = surveyOrderId,
            MissionId = missionId,
            FarmBoundaryVersionId = farmBoundaryVersionId,
            FarmBaseMapVersionId = farmBaseMapVersionId,
            ExpectedCurrentFarmBaseMapVersionId = expectedCurrentFarmBaseMapVersionId,
            GridBearingDeg = gridBearingDeg,
            RowSpacingM = rowSpacingM,
            PlantSpacingM = plantSpacingM,
            JobId = jobId,
            ModelVersionId = modelVersionId,
            ModelVersion = modelVersion?.Trim() ?? string.Empty,
            AlgorithmVersion = algorithmVersion?.Trim() ?? string.Empty,
            BoundaryPolicyVersion = boundaryPolicyVersion?.Trim() ?? string.Empty,
            ProducedAt = producedAt,
            SpatialPolicyVersionId = spatialPolicyVersionId,
            SpatialPolicyFingerprint = spatialPolicyFingerprint.Trim(),
            BoundaryToleranceM = boundaryToleranceM,
            MatchingDistanceThresholdM = matchingDistanceThresholdM,
            RowBearingToleranceDeg = rowBearingToleranceDeg,
            GridSpacingToleranceM = gridSpacingToleranceM,
            Status = MappingCandidateBatchStatus.PendingReview,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void LinkFarmBaseMapVersion(Guid farmBaseMapVersionId)
    {
        DomainGuard.NotEmpty(farmBaseMapVersionId);
        FarmBaseMapVersionId = farmBaseMapVersionId;
    }

    public void AddCandidate(MappingCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        _candidates.Add(candidate);
    }

    public void StartReview(Guid reviewerId, DateTimeOffset reviewedAt)
    {
        DomainGuard.NotEmpty(reviewerId);
        DomainGuard.Utc(reviewedAt);

        if (Status != MappingCandidateBatchStatus.PendingReview)
        {
            throw new InvalidOperationException($"Cannot start review on batch with status '{Status}'.");
        }

        Status = MappingCandidateBatchStatus.ReviewInProgress;
        ReviewedBy = reviewerId;
        ReviewedAt = reviewedAt;
        UpdatedAt = reviewedAt;
        Version++;
    }

    public void Approve(Guid reviewerId, string reason, DateTimeOffset reviewedAt)
    {
        DomainGuard.NotEmpty(reviewerId);
        DomainGuard.Utc(reviewedAt);

        if (Status is not (MappingCandidateBatchStatus.PendingReview or MappingCandidateBatchStatus.ReviewInProgress))
        {
            throw new InvalidOperationException($"Cannot approve batch with status '{Status}'.");
        }

        Status = MappingCandidateBatchStatus.Approved;
        ReviewedBy = reviewerId;
        ReviewedAt = reviewedAt;
        ReviewReason = reason?.Trim();
        UpdatedAt = reviewedAt;
        Version++;
    }

    public void Reject(Guid reviewerId, string reason, DateTimeOffset reviewedAt)
    {
        DomainGuard.NotEmpty(reviewerId);
        DomainGuard.Utc(reviewedAt);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A rejection reason is required.", nameof(reason));
        }

        if (Status is not (MappingCandidateBatchStatus.PendingReview or MappingCandidateBatchStatus.ReviewInProgress))
        {
            throw new InvalidOperationException($"Cannot reject batch with status '{Status}'.");
        }

        Status = MappingCandidateBatchStatus.Rejected;
        ReviewedBy = reviewerId;
        ReviewedAt = reviewedAt;
        ReviewReason = reason.Trim();
        UpdatedAt = reviewedAt;
        Version++;
    }

    public void MarkApplied(DateTimeOffset appliedAt)
    {
        DomainGuard.Utc(appliedAt);

        if (Status != MappingCandidateBatchStatus.Approved)
        {
            throw new InvalidOperationException($"Only an approved batch can be marked as applied. Current status: '{Status}'.");
        }

        Status = MappingCandidateBatchStatus.Applied;
        UpdatedAt = appliedAt;
        Version++;
    }
}
