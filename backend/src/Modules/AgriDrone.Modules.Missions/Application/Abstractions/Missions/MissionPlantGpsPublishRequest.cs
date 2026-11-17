using AgriDrone.IntegrationContracts.Mapping;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public sealed record MissionPlantGpsPublishRequest(
    Guid SurveyOrderId,
    double RowSpacingM,
    double PlantSpacingM,
    double GridBearingDeg,
    IReadOnlyList<MissionPlantGpsCandidateInput> Candidates,
    Guid? CaptureAttemptId = null,
    Guid? FarmBoundaryVersionId = null,
    Guid? HandoffId = null,
    Guid? JobId = null,
    Guid? ModelVersionId = null,
    Guid? ExpectedCurrentFarmBaseMapVersionId = null,
    MappingKind? MappingKind = null);

