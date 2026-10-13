namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record SurveyOrderReadinessApiResponse(
    Guid SurveyOrderId,
    Guid MissionId,
    Guid TenantId,
    Guid FarmId,
    string RequestedMissionPurpose,
    uint OrderVersion,
    Guid? AppointmentId,
    Guid? SystemManagerProfileId,
    bool IsReady,
    IReadOnlyList<string> ReadinessFailures,
    DateTimeOffset EvaluatedAt);
