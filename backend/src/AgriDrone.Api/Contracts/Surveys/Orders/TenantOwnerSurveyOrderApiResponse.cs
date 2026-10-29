namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record TenantOwnerSurveyOrderApiResponse(
    Guid SurveyOrderId,
    string OrderNumber,
    Guid FarmId,
    string FarmName,
    string Status,
    uint OrderVersion,
    decimal DepositAmount,
    decimal? TotalAmount,
    decimal? RemainingAmount,
    string Currency,
    TenantOwnerAppointmentApiResponse? CurrentAppointment);

public sealed record TenantOwnerAppointmentApiResponse(
    Guid AppointmentId,
    string Purpose,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    uint AppointmentVersion);
