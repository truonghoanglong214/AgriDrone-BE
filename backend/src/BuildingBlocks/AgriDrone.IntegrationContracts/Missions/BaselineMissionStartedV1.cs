namespace AgriDrone.IntegrationContracts.Missions;

public sealed record BaselineMissionStartedV1(
    Guid SurveyOrderId,
    Guid MissionId,
    Guid TenantId,
    Guid FarmId,
    DateTimeOffset StartedAt);
