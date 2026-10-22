namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record SystemAdminSurveyOrderApiResponse(
    Guid SurveyOrderId,
    string OrderNumber,
    Guid TenantId,
    Guid FarmId,
    string FarmCode,
    string FarmName,
    Guid SurveyServiceId,
    string SurveyServiceCode,
    string SurveyServiceName,
    string Status,
    uint OrderVersion,
    bool RequiresBaselineMapping,
    Guid? FarmBoundaryVersionId,
    Guid? BoundaryCaptureSessionId,
    Guid? BaselineRoutePlanId,
    Guid? CurrentAppointmentId,
    uint? CurrentAppointmentVersion,
    string? CurrentAppointmentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
