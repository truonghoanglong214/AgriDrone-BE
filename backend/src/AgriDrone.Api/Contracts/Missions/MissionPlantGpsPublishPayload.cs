namespace AgriDrone.Api.Contracts.Missions;

public sealed record MissionPlantGpsPublishPayload(
    Guid SurveyOrderId,
    Guid ProcessingJobId);
