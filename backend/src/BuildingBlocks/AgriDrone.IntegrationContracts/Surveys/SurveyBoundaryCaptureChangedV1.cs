namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyBoundaryCaptureChangedV1(
    Guid SurveyOrderId,
    Guid BoundaryCaptureSessionId,
    Guid TenantId,
    Guid FarmId,
    Guid AppointmentId,
    Guid DroneId,
    string CaptureStatus,
    Guid? FarmBoundaryVersionId,
    Guid? BaselineRoutePlanId,
    string OrderStatus,
    int SampleCount,
    string ChangeType,
    DateTimeOffset ChangedAt);
