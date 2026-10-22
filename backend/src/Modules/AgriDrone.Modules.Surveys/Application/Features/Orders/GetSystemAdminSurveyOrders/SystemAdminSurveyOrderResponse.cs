using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSystemAdminSurveyOrders;

public sealed record SystemAdminSurveyOrderResponse(
    Guid SurveyOrderId,
    string OrderNumber,
    Guid TenantId,
    Guid FarmId,
    string FarmCode,
    string FarmName,
    Guid SurveyServiceId,
    string SurveyServiceCode,
    string SurveyServiceName,
    SurveyOrderStatus Status,
    uint OrderVersion,
    bool RequiresBaselineMapping,
    Guid? FarmBoundaryVersionId,
    Guid? BoundaryCaptureSessionId,
    Guid? BaselineRoutePlanId,
    Guid? CurrentAppointmentId,
    uint? CurrentAppointmentVersion,
    SurveyAppointmentStatus? CurrentAppointmentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
