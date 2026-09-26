namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record BaselineMappingCandidatesApprovedV2(
    Guid CausationId,
    Guid ApprovalId,
    Guid SurveyOrderId,
    Guid MissionId,
    Guid FarmId,
    Guid? ExpectedCurrentFarmBaseMapVersionId,
    string AlgorithmVersion,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<ZoneMappingCandidatesV2> Zones);

public sealed record ZoneMappingCandidatesV2(
    Guid ZoneId,
    Guid? ExpectedCurrentZoneMapVersionId,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    IReadOnlyList<MappingCandidateV1> Candidates);
