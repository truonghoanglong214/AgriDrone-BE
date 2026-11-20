namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record MappingCandidatesReadyV4(
    MappingKind MappingKind,
    Guid SurveyOrderId,
    Guid MissionId,
    Guid CaptureAttemptId,
    Guid FarmId,
    Guid FarmBoundaryVersionId,
    Guid HandoffId,
    Guid JobId,
    Guid ModelVersionId,
    Guid? ExpectedCurrentFarmBaseMapVersionId,
    DateTimeOffset ProducedAt,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    IReadOnlyList<MappingCandidateV3> Candidates,
    string ModelVersion,
    string AlgorithmVersion,
    string? BoundaryPolicyVersion = null,
    Guid? CausationId = null);
