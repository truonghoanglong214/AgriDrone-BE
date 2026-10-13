using AgriDrone.IntegrationContracts.Mapping;

namespace AgriDrone.IntegrationContracts.Health;

public sealed record PlantHealthAnalysisReadyV3(
    Guid CausationId,
    Guid HandoffId,
    Guid SurveyOrderId,
    Guid MissionId,
    Guid FarmId,
    Guid FarmBoundaryVersionId,
    Guid FarmBaseMapVersionId,
    Guid JobId,
    Guid ModelVersionId,
    string ModelVersion,
    Guid? ThresholdProfileId,
    string? ThresholdProfileVersion,
    string BoundaryPolicyVersion,
    DateTimeOffset ProducedAt,
    IReadOnlyList<PlantHealthObservationV3> Observations,
    IReadOnlyList<HealthBoundaryExceptionV3> BoundaryExceptions,
    IReadOnlyList<ProposedDiseaseZoneV3> DiseaseZones);

public sealed record PlantHealthObservationV3(
    Guid ObservationId,
    int ObservationVersion,
    Guid ZoneId,
    Guid? PlantId,
    Guid? MappingCandidateId,
    GeoJsonPointV3 Position,
    DateTimeOffset ObservedAt,
    string ConditionCode,
    string ConditionDefinitionVersion,
    string HealthLevelCode,
    decimal SeverityPercent,
    decimal Confidence,
    IReadOnlyList<EvidenceMediaReferenceV3> EvidenceMedia);

public sealed record EvidenceMediaReferenceV3(
    Guid MediaAssetId,
    string Role);

public sealed record HealthBoundaryExceptionV3(
    string SourceReferenceId,
    GeoJsonPointV3 OriginalPosition,
    string BoundaryClassification,
    decimal MeasuredBoundaryDistanceM,
    decimal BoundaryThresholdM,
    string BoundaryPolicyVersion,
    IReadOnlyList<EvidenceMediaReferenceV3> EvidenceMedia);

public sealed record ProposedDiseaseZoneV3(
    string SourceProposalId,
    GeoJsonPolygonV3 Geometry,
    string ConditionCode,
    string ConditionDefinitionVersion,
    string HealthLevelCode,
    int MembershipVersion,
    IReadOnlyList<ProposedDiseaseZoneMembershipV3> Memberships,
    IReadOnlyList<TreatmentRecommendationCandidateV3> RecommendationCandidates,
    IReadOnlyList<EvidenceMediaReferenceV3> EvidenceMedia);

public sealed record ProposedDiseaseZoneMembershipV3(
    Guid? MappingCandidateId,
    Guid? PlantId,
    decimal Confidence);

public sealed record TreatmentRecommendationCandidateV3(
    Guid RecommendationId,
    string Code,
    int VersionNumber);
