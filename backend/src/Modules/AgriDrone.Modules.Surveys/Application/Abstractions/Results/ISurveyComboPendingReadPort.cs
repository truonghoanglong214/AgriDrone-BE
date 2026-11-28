using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Results;

public sealed record SurveyComboPendingReviewBatch(
    Guid BatchId,
    Guid TenantId,
    Guid FarmId,
    Guid SurveyOrderId,
    Guid CaptureAttemptId,
    Guid MissionId,
    Guid JobId,
    int SourceRevision,
    Guid HandoffId,
    Guid? PredecessorBatchId,
    Guid? SupersedesHandoffId,
    Guid BusinessPolicyVersionId,
    string MappingKind,
    Guid MappingCandidateBatchId,
    Guid FarmBaseMapVersionId,
    Guid BaselineMapVersionId,
    Guid FarmBoundaryVersionId,
    string CanonicalFingerprint,
    DateTimeOffset ProducedAt,
    DateTimeOffset ImportedAt,
    int BlockingCount,
    IReadOnlyList<string> BlockingReasons,
    string Status,
    string ResultNature,
    string LimitationNotice,
    IReadOnlyList<ComboPendingFindingDto> Findings,
    IReadOnlyList<ComboPendingBoundaryExceptionDto> BoundaryExceptions,
    IReadOnlyList<ComboPendingDiseaseZoneDto> DiseaseZones);

public sealed record ComboPendingFindingDto(
    Guid FindingId,
    int FindingVersion,
    Guid? PlantProfileId,
    Guid? MappingCandidateId,
    double Longitude,
    double Latitude,
    DateTimeOffset ObservedAt,
    string SuspectedClassCode,
    string ClassificationNature,
    decimal Confidence,
    string Uncertainty,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<Guid> EvidenceMediaIds,
    Guid? PlantId = null);

public sealed record ComboPendingBoundaryExceptionDto(
    string SourceReferenceId,
    double Longitude,
    double Latitude,
    string BoundaryClassification,
    decimal MeasuredBoundaryDistanceM,
    decimal BoundaryThresholdM,
    IReadOnlyList<Guid> EvidenceMediaIds);

public sealed record ComboPendingDiseaseZoneDto(
    string SourceProposalId,
    GeoJsonPolygonV3 Geometry,
    string SuspectedClassCode,
    int MembershipVersion,
    IReadOnlyList<ComboPendingZoneMembershipDto> Memberships,
    IReadOnlyList<ComboPendingGuidanceCandidateDto> GuidanceCandidates,
    IReadOnlyList<Guid> EvidenceMediaIds);

public sealed record ComboPendingGuidanceCandidateDto(
    Guid GuidanceId,
    int VersionNumber,
    string Code,
    string SourceReference);

public sealed record ComboPendingZoneMembershipDto(
    Guid FindingId,
    Guid? PlantProfileId,
    Guid? MappingCandidateId,
    decimal Confidence);

public sealed record SurveyComboReviewSummaryDto(
    Guid ReviewRevisionId,
    int RevisionNumber,
    Guid? PredecessorReviewId,
    string Outcome,
    string ReviewReason,
    bool ConfirmNoDiseaseObserved,
    string? NegativeResultConfirmationEvidence,
    Guid ReviewedBy,
    DateTimeOffset ReviewedAt,
    string DecisionFingerprint,
    int FindingsCount,
    int AcceptedFindingsCount,
    int ExcludedFindingsCount,
    int ZonesCount,
    int ApprovedZonesCount,
    int RejectedZonesCount,
    int BlockersCount,
    int ResolvedBlockersCount,
    int UnresolvedBlockersCount,
    IReadOnlyList<ComboFindingReviewDecisionRecord> FindingDecisions,
    IReadOnlyList<ComboZoneReviewDecisionRecord> ZoneDecisions,
    IReadOnlyList<ComboBlockerResolutionRecord> BlockerResolutions);

public sealed record SurveyExpectedFreezeTokensDto(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid ExpectedAttemptId,
    Guid ExpectedCountSnapshotId,
    string ExpectedCountFingerprint,
    Guid? ExpectedComboBatchId,
    int? ExpectedComboSourceRevision,
    string? ExpectedComboBatchFingerprint,
    Guid? ExpectedComboReviewRevisionId,
    string? ExpectedComboReviewFingerprint);

public sealed record SurveyComboReviewReadContext(
    SurveyComboPendingReviewBatch Batch,
    Guid VerifiedPoleCountSnapshotId,
    int VerifiedBillablePoleCount,
    string CountSnapshotFingerprint,
    int CountSnapshotRevision,
    SurveyComboReviewSummaryDto? CurrentReview,
    SurveyExpectedFreezeTokensDto ExpectedTokens);

public interface ISurveyComboPendingReadPort
{
    Task<Result<SurveyComboPendingReviewBatch>> GetPendingBatchForReviewAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<Result<SurveyComboPendingReviewBatch>> GetPendingBatchForReviewAsync(
        Guid tenantId,
        Guid farmId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<Result<SurveyComboPendingReviewBatch>> GetBatchByIdAsync(
        Guid orderId,
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<Result<SurveyComboReviewReadContext>> GetComboReviewContextAsync(
        Guid tenantId,
        Guid farmId,
        Guid orderId,
        CancellationToken cancellationToken = default);
}
