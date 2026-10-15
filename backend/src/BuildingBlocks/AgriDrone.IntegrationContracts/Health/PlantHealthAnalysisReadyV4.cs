using AgriDrone.IntegrationContracts.Mapping;

namespace AgriDrone.IntegrationContracts.Health;

/// <summary>
/// No-severity Plant Health handoff. All classes are suspected/team-labelled;
/// publication remains a reviewed preliminary result, never a diagnosis.
/// </summary>
public sealed record PlantHealthAnalysisReadyV4(
    Guid CausationId,
    Guid HandoffId,
    Guid SurveyOrderId,
    Guid MissionId,
    Guid FarmId,
    Guid FarmBoundaryVersionId,
    Guid FarmBaseMapVersionId,
    Guid JobId,
    DateTimeOffset ProducedAt,
    string ResultNature,
    string TeamLabelLimitation,
    PlantHealthProvenanceV4 Provenance,
    IReadOnlyList<PlantHealthFindingV4> Findings,
    IReadOnlyList<HealthBoundaryExceptionV4> BoundaryExceptions,
    IReadOnlyList<ProposedDiseaseZoneV4> DiseaseZones);

public sealed record PlantHealthProvenanceV4(
    string DatasetId,
    string DatasetVersion,
    string AnnotationRuleVersion,
    Guid ModelVersionId,
    string ModelVersion,
    string? ThresholdProfileVersion,
    string BoundaryPolicyVersion);

public sealed record PlantHealthFindingV4(
    Guid FindingId,
    int FindingVersion,
    Guid? PlantId,
    Guid? MappingCandidateId,
    GeoJsonPointV3 Position,
    DateTimeOffset ObservedAt,
    string SuspectedClassCode,
    string ClassificationNature,
    decimal Confidence,
    string Uncertainty,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<EvidenceMediaReferenceV4> EvidenceMedia);

public sealed record EvidenceMediaReferenceV4(Guid MediaAssetId, string Role);

public sealed record HealthBoundaryExceptionV4(
    string SourceReferenceId,
    GeoJsonPointV3 OriginalPosition,
    string BoundaryClassification,
    decimal MeasuredBoundaryDistanceM,
    decimal BoundaryThresholdM,
    IReadOnlyList<EvidenceMediaReferenceV4> EvidenceMedia);

public sealed record ProposedDiseaseZoneV4(
    string SourceProposalId,
    GeoJsonPolygonV3 Geometry,
    string SuspectedClassCode,
    int MembershipVersion,
    IReadOnlyList<ProposedDiseaseZoneMembershipV4> Memberships,
    IReadOnlyList<SourcedGuidanceReferenceV4> GuidanceCandidates,
    IReadOnlyList<EvidenceMediaReferenceV4> EvidenceMedia);

public sealed record ProposedDiseaseZoneMembershipV4(
    Guid? MappingCandidateId,
    Guid? PlantId,
    decimal Confidence);

public sealed record SourcedGuidanceReferenceV4(
    Guid GuidanceId,
    string Code,
    int VersionNumber,
    string SourceReference);
