namespace AgriDrone.Api.Contracts.Missions;

public sealed record MissionPlantGpsPublishPayload(
    Guid SurveyOrderId,
    IReadOnlyList<MissionPlantGpsPublishCandidate> Candidates,
    double RowSpacingM = 3.0,
    double PlantSpacingM = 2.5,
    double GridBearingDeg = 0.0);
