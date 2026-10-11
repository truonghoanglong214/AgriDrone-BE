namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderReadiness;

public sealed record SurveyOrderReadinessResponse(
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
