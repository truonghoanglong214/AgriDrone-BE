namespace AgriDrone.Api.Contracts.Missions;

public sealed record PublishMissionPlantGpsRequest(
    MissionPlantGpsPublishPayload Payload);

public sealed record MissionPlantGpsPublishPayload(
    Guid SurveyOrderId,
    IReadOnlyList<MissionPlantGpsPublishZone> Zones);

public sealed record MissionPlantGpsPublishZone(
    Guid ZoneId,
    double RowSpacingM,
    double PlantSpacingM,
    double GridBearingDeg,
    IReadOnlyList<MissionPlantGpsPublishCandidate> Candidates);

public sealed record MissionPlantGpsPublishCandidate(string TreeId);
