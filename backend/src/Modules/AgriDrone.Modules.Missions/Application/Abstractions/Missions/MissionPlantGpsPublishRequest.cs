namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public sealed record MissionPlantGpsPublishRequest(
    Guid SurveyOrderId,
    double RowSpacingM,
    double PlantSpacingM,
    double GridBearingDeg,
    IReadOnlyList<MissionPlantGpsCandidateInput> Candidates);
