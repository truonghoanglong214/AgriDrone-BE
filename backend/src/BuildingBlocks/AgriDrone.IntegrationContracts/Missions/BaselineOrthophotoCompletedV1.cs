namespace AgriDrone.IntegrationContracts.Missions;

public sealed record BaselineOrthophotoCompletedV1(
    Guid SurveyOrderId,
    Guid MissionId,
    Guid TenantId,
    Guid FarmId,
    string? OrthophotoUrl,
    DateTimeOffset CompletedAt);
