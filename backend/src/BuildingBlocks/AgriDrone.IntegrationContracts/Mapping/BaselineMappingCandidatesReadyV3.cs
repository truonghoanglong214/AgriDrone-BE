namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record BaselineMappingCandidatesReadyV3(
    Guid SurveyOrderId,
    Guid MissionId,
    Guid FarmId,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    IReadOnlyList<MappingCandidateV3> Candidates,
    Guid? FarmBoundaryVersionId = null,
    Guid? CausationId = null,
    Guid? HandoffId = null,
    Guid? ExpectedCurrentFarmBaseMapVersionId = null,
    Guid? JobId = null,
    Guid? ModelVersionId = null,
    string ModelVersion = "yolo-v8",
    string AlgorithmVersion = "orchard-grid-v1",
    string BoundaryPolicyVersion = "v1",
    DateTimeOffset? ProducedAt = null);
