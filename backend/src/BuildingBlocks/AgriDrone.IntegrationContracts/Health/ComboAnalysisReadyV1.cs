using AgriDrone.IntegrationContracts.Mapping;

namespace AgriDrone.IntegrationContracts.Health;

/// <summary>
/// V3 Unified COMBO Analysis handoff.
/// Contains verified spatial plant identity (PlantProfileId/MappingCandidateId),
/// disease observations, boundary exceptions, and proposed disease zones.
/// Publication remains a reviewed preliminary result, never a diagnosis.
/// </summary>
public sealed record ComboAnalysisReadyV1(
    Guid CausationId,
    Guid HandoffId,
    Guid SurveyOrderId,
    Guid FarmId,
    Guid CaptureAttemptId,
    Guid MissionId,
    Guid JobId,
    int SourceRevision,
    DateTimeOffset ProducedAt,
    Guid? SupersedesHandoffId,
    Guid BusinessPolicyVersionId,
    Guid? InputApprovalId,
    Guid? InputManifestId,
    string? InputManifestHash,
    MappingKind MappingKind,
    Guid MappingCandidateBatchId,
    Guid FarmBaseMapVersionId,
    Guid BaselineMapVersionId,
    Guid FarmBoundaryVersionId,
    string ResultNature,
    string LimitationNotice,
    ComboAnalysisProvenanceV1 Provenance,
    IReadOnlyList<ComboFindingV1> Findings,
    IReadOnlyList<ComboBoundaryExceptionV1> BoundaryExceptions,
    IReadOnlyList<ProposedComboDiseaseZoneV1> DiseaseZones);

public sealed record ComboAnalysisProvenanceV1(
    string DatasetId,
    string DatasetVersion,
    string AnnotationRuleVersion,
    Guid ModelVersionId,
    string ModelVersion,
    string? ThresholdProfileVersion,
    string BoundaryPolicyVersion);

public sealed record ComboFindingV1(
    Guid FindingId,
    int FindingVersion,
    Guid? PlantProfileId,
    Guid? MappingCandidateId,
    GeoJsonPointV3 Position,
    DateTimeOffset ObservedAt,
    string SuspectedClassCode,
    string ClassificationNature,
    decimal Confidence,
    string Uncertainty,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<EvidenceMediaReferenceV4> EvidenceMedia);

public sealed record ComboBoundaryExceptionV1(
    string SourceReferenceId,
    GeoJsonPointV3 OriginalPosition,
    string BoundaryClassification,
    decimal MeasuredBoundaryDistanceM,
    decimal BoundaryThresholdM,
    IReadOnlyList<EvidenceMediaReferenceV4> EvidenceMedia);

public sealed record ProposedComboDiseaseZoneV1(
    string SourceProposalId,
    GeoJsonPolygonV3 Geometry,
    string SuspectedClassCode,
    int MembershipVersion,
    IReadOnlyList<ProposedDiseaseZoneMembershipV1> Memberships,
    IReadOnlyList<SourcedGuidanceReferenceV4> GuidanceCandidates,
    IReadOnlyList<EvidenceMediaReferenceV4> EvidenceMedia);

public sealed record ProposedDiseaseZoneMembershipV1(
    Guid FindingId,
    Guid? PlantProfileId,
    Guid? MappingCandidateId,
    decimal Confidence);

public static class ComboAnalysisV1Values
{
    public const string ResultNature = "PRELIMINARY_SUSPECTED";
    public const string ClassificationNature = "SUSPECTED";
    public const decimal BoundaryReviewThresholdMeters = 1m;
}
