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
    public Guid SourceCandidateId { get; private set; }
    public Guid? PlantProfileId { get; private set; }
    public Point EffectivePosition { get; private set; } = null!;
    public string ReviewDecision { get; private set; } = null!;
    public bool IsAccepted { get; private set; }
    public bool IsStrictlyInBoundary { get; private set; }
    public string? ExclusionReason { get; private set; }
    public Guid? TargetPlantId { get; private set; }
    public Guid? TargetPlantProfileId { get; private set; }
    public Point? PreviousPosition { get; private set; }
    public int? EffectiveRowIndex { get; private set; }
    public int? EffectiveColumnIndex { get; private set; }

    public static SurveyOrderPoleCountSnapshotItem Create(
        Guid id,
        Guid snapshotId,
        Guid tenantId,
        Guid farmId,
        Guid mappingCandidateBatchId,
        Guid mappingCandidateId,
        Guid sourceCandidateId,
        Guid? plantProfileId,
        Point effectivePosition,
        string reviewDecision,
        bool isAccepted,
        bool isStrictlyInBoundary,
        string? exclusionReason = null,
        Guid? targetPlantId = null,
        Guid? targetPlantProfileId = null,
        Point? previousPosition = null,
        int? effectiveRowIndex = null,
        int? effectiveColumnIndex = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(snapshotId, nameof(snapshotId));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(mappingCandidateBatchId, nameof(mappingCandidateBatchId));
        DomainGuard.NotEmpty(mappingCandidateId, nameof(mappingCandidateId));
        DomainGuard.NotEmpty(sourceCandidateId, nameof(sourceCandidateId));
        ArgumentNullException.ThrowIfNull(effectivePosition, nameof(effectivePosition));
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewDecision, nameof(reviewDecision));

        var decision = reviewDecision.Trim();

        if (isAccepted && !plantProfileId.HasValue)
        {
            throw new InvalidOperationException("Accepted snapshot item must have a linked PlantProfileId.");
        }

        if (isAccepted && !string.IsNullOrWhiteSpace(exclusionReason))
        {
            throw new InvalidOperationException("Accepted snapshot item cannot have an ExclusionReason.");
        }

        if (!isAccepted && string.IsNullOrWhiteSpace(exclusionReason))
        {
            throw new InvalidOperationException("Rejected/Excluded snapshot item must provide an ExclusionReason.");
        }

        if (!isAccepted && plantProfileId.HasValue)
        {
            throw new InvalidOperationException("Rejected/Excluded snapshot item cannot have a resultant PlantProfileId.");
        }

        if (decision is "MatchExisting" or "Relocate" or "Replace")
        {
            if (!targetPlantId.HasValue || targetPlantId.Value == Guid.Empty)
            {
                throw new InvalidOperationException($"Snapshot item with decision '{decision}' requires a non-empty TargetPlantId.");
            }

            if (!targetPlantProfileId.HasValue || targetPlantProfileId.Value == Guid.Empty)
            {
                throw new InvalidOperationException($"Snapshot item with decision '{decision}' requires a non-empty TargetPlantProfileId.");
            }
        }

        if (decision is "Relocate" or "Replace" && previousPosition is null)
        {
            throw new InvalidOperationException($"Snapshot item with decision '{decision}' requires PreviousPosition.");
        }

        if (isAccepted && decision is "CreateNew" or "Relocate" or "Replace")
        {
            if (!effectiveRowIndex.HasValue || effectiveRowIndex.Value < 1)
            {
                throw new InvalidOperationException($"Accepted snapshot item with decision '{decision}' requires EffectiveRowIndex >= 1.");
            }

            if (!effectiveColumnIndex.HasValue || effectiveColumnIndex.Value < 1)
            {
                throw new InvalidOperationException($"Accepted snapshot item with decision '{decision}' requires EffectiveColumnIndex >= 1.");
            }
        }

        return new SurveyOrderPoleCountSnapshotItem
        {
            Id = id,
            SnapshotId = snapshotId,
            TenantId = tenantId,
            FarmId = farmId,
            MappingCandidateBatchId = mappingCandidateBatchId,
            MappingCandidateId = mappingCandidateId,
            SourceCandidateId = sourceCandidateId,
            PlantProfileId = plantProfileId,
            EffectivePosition = effectivePosition,
            ReviewDecision = decision,
            IsAccepted = isAccepted,
            IsStrictlyInBoundary = isStrictlyInBoundary,
            ExclusionReason = exclusionReason?.Trim(),
            TargetPlantId = targetPlantId,
            TargetPlantProfileId = targetPlantProfileId,
            PreviousPosition = previousPosition,
            EffectiveRowIndex = effectiveRowIndex,
            EffectiveColumnIndex = effectiveColumnIndex
        };
    }
}
