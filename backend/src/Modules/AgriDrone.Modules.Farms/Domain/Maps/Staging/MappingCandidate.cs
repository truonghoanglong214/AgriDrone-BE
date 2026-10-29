using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Domain.Maps.Staging;

public sealed class MappingCandidate : AggregateRoot
{
    private MappingCandidate() { }

    public Guid BatchId { get; private set; }
    public Guid? ZoneId { get; private set; }
    public Guid CandidateId { get; private set; }
    public Guid SourceObservationId { get; private set; }
    public IReadOnlyList<Guid> EvidenceMediaAssetIds { get; private set; } = [];
    public Point OriginalPosition { get; private set; } = null!;
    public int? ProposedRowIndex { get; private set; }
    public int? ProposedColumnIndex { get; private set; }
    public decimal PositionConfidence { get; private set; }
    public string BoundaryClassification { get; private set; } = string.Empty;
    public decimal MeasuredBoundaryDistanceM { get; private set; }
    public decimal BoundaryThresholdM { get; private set; }
    public Guid? ProposedMatchedPlantId { get; private set; }
    public decimal? MatchConfidence { get; private set; }
    public string? TreeId { get; private set; }

    public MappingCandidateReviewDecision ReviewDecision { get; private set; }
    public Point? CorrectedPosition { get; private set; }
    public int? CorrectedRowIndex { get; private set; }
    public int? CorrectedColumnIndex { get; private set; }
    public Guid? CorrectedMatchedPlantId { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewReason { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public MappingCandidateBatch Batch { get; private set; } = null!;

    public static MappingCandidate Create(
        Guid id,
        Guid batchId,
        Guid? zoneId,
        Guid candidateId,
        Guid sourceObservationId,
        IReadOnlyList<Guid> evidenceMediaAssetIds,
        Point originalPosition,
        int? proposedRowIndex,
        int? proposedColumnIndex,
        decimal positionConfidence,
        string boundaryClassification,
        decimal measuredBoundaryDistanceM,
        decimal boundaryThresholdM,
        Guid? proposedMatchedPlantId,
        decimal? matchConfidence,
        DateTimeOffset createdAt,
        string? treeId = null)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(batchId);
        if (zoneId.HasValue) DomainGuard.NotEmpty(zoneId.Value);
        DomainGuard.NotEmpty(candidateId);
        DomainGuard.NotEmpty(sourceObservationId);
        ArgumentNullException.ThrowIfNull(originalPosition);
        DomainGuard.Utc(createdAt);

        if (proposedRowIndex.HasValue) ArgumentOutOfRangeException.ThrowIfLessThan(proposedRowIndex.Value, 1);
        if (proposedColumnIndex.HasValue) ArgumentOutOfRangeException.ThrowIfLessThan(proposedColumnIndex.Value, 1);

        return new MappingCandidate
        {
            Id = id,
            BatchId = batchId,
            ZoneId = zoneId,
            CandidateId = candidateId,
            SourceObservationId = sourceObservationId,
            EvidenceMediaAssetIds = evidenceMediaAssetIds ?? [],
            OriginalPosition = (Point)originalPosition.Copy(),
            ProposedRowIndex = proposedRowIndex,
            ProposedColumnIndex = proposedColumnIndex,
            PositionConfidence = positionConfidence,
            BoundaryClassification = boundaryClassification?.Trim() ?? string.Empty,
            MeasuredBoundaryDistanceM = measuredBoundaryDistanceM,
            BoundaryThresholdM = boundaryThresholdM,
            ProposedMatchedPlantId = proposedMatchedPlantId,
            MatchConfidence = matchConfidence,
            TreeId = treeId?.Trim(),
            ReviewDecision = MappingCandidateReviewDecision.Pending,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void ApplyReview(
        MappingCandidateReviewDecision decision,
        Point? correctedPosition,
        int? correctedRowIndex,
        int? correctedColumnIndex,
        Guid? correctedMatchedPlantId,
        Guid reviewerId,
        string? reason,
        DateTimeOffset reviewedAt)
    {
        DomainGuard.NotEmpty(reviewerId);
        DomainGuard.Utc(reviewedAt);

        if (decision == MappingCandidateReviewDecision.Pending)
        {
            throw new ArgumentException("Cannot review a candidate to 'Pending' status.", nameof(decision));
        }

        if (decision == MappingCandidateReviewDecision.Relocate && correctedPosition is null)
        {
            throw new InvalidOperationException("Relocate decision requires a corrected position.");
        }

        if (decision is MappingCandidateReviewDecision.Relocate or MappingCandidateReviewDecision.MatchExisting or MappingCandidateReviewDecision.Replace &&
            !correctedMatchedPlantId.HasValue && !ProposedMatchedPlantId.HasValue)
        {
            throw new InvalidOperationException($"{decision} decision requires an existing plant ID to match or replace.");
        }

        if (correctedRowIndex.HasValue) ArgumentOutOfRangeException.ThrowIfLessThan(correctedRowIndex.Value, 1);
        if (correctedColumnIndex.HasValue) ArgumentOutOfRangeException.ThrowIfLessThan(correctedColumnIndex.Value, 1);

        ReviewDecision = decision;
        CorrectedPosition = correctedPosition is not null ? (Point)correctedPosition.Copy() : null;
        CorrectedRowIndex = correctedRowIndex;
        CorrectedColumnIndex = correctedColumnIndex;
        CorrectedMatchedPlantId = correctedMatchedPlantId ?? ProposedMatchedPlantId;
        ReviewedBy = reviewerId;
        ReviewedAt = reviewedAt;
        ReviewReason = reason?.Trim();
        UpdatedAt = reviewedAt;
        Version++;
    }
}
