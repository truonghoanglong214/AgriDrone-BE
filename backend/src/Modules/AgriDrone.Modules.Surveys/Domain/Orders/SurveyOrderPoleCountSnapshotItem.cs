using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyOrderPoleCountSnapshotItem : Entity
{
    private SurveyOrderPoleCountSnapshotItem() { }

    public Guid SnapshotId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid MappingCandidateBatchId { get; private set; }
    public Guid MappingCandidateId { get; private set; }
    public Guid? PlantProfileId { get; private set; }
    public Point EffectivePosition { get; private set; } = null!;
    public string ReviewDecision { get; private set; } = null!;
    public bool IsAccepted { get; private set; }
    public bool IsStrictlyInBoundary { get; private set; }
    public string? ExclusionReason { get; private set; }

    public static SurveyOrderPoleCountSnapshotItem Create(
        Guid id,
        Guid snapshotId,
        Guid tenantId,
        Guid farmId,
        Guid mappingCandidateBatchId,
        Guid mappingCandidateId,
        Guid? plantProfileId,
        Point effectivePosition,
        string reviewDecision,
        bool isAccepted,
        bool isStrictlyInBoundary,
        string? exclusionReason = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(snapshotId, nameof(snapshotId));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(mappingCandidateBatchId, nameof(mappingCandidateBatchId));
        DomainGuard.NotEmpty(mappingCandidateId, nameof(mappingCandidateId));
        ArgumentNullException.ThrowIfNull(effectivePosition, nameof(effectivePosition));
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewDecision, nameof(reviewDecision));

        if (isAccepted && !plantProfileId.HasValue)
        {
            throw new InvalidOperationException("Accepted snapshot item must have a linked PlantProfileId.");
        }

        if (!isAccepted && string.IsNullOrWhiteSpace(exclusionReason))
        {
            throw new InvalidOperationException("Rejected/Excluded snapshot item must provide an ExclusionReason.");
        }

        return new SurveyOrderPoleCountSnapshotItem
        {
            Id = id,
            SnapshotId = snapshotId,
            TenantId = tenantId,
            FarmId = farmId,
            MappingCandidateBatchId = mappingCandidateBatchId,
            MappingCandidateId = mappingCandidateId,
            PlantProfileId = plantProfileId,
            EffectivePosition = effectivePosition,
            ReviewDecision = reviewDecision.Trim(),
            IsAccepted = isAccepted,
            IsStrictlyInBoundary = isStrictlyInBoundary,
            ExclusionReason = exclusionReason?.Trim()
        };
    }
}
