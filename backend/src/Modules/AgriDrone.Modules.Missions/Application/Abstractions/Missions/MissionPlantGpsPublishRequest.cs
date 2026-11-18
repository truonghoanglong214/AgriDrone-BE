namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public sealed record MissionPlantGpsPublishRequest(
    Guid SurveyOrderId,
    Guid ProcessingJobId);
