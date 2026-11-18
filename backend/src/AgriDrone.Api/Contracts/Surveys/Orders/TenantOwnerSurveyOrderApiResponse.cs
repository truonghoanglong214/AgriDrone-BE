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
    TenantOwnerAppointmentApiResponse? CurrentAppointment,
    string ServiceType,
    int? ConfirmedMappingPoleCount = null,
    int? ConfirmedPlantHealthPoleCount = null,
    decimal? MappingPricePerPole = null,
    decimal? PlantHealthPricePerPole = null,
    decimal? MappingSubtotal = null,
    decimal? PlantHealthSubtotal = null,
    decimal? ComboDiscountPercentage = null,
    decimal? ComboDiscountAmount = null);

public sealed record TenantOwnerAppointmentApiResponse(
    Guid AppointmentId,
    string Purpose,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    uint AppointmentVersion,
    DateOnly? ScheduledDate = null,
    DateOnly? ScheduledStartDate = null,
    DateOnly? ScheduledEndDate = null);
