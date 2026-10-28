using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

public interface ISystemManagerSurveyOrderQueries
{
    Task<PagedResult<SystemManagerSurveyOrderItem>?> GetAssignedAsync(
        Guid actorId,
        SystemManagerSurveyOrderFilter filter,
        PagedRequest page,
        CancellationToken cancellationToken = default);

    Task<PagedResult<SystemManagerSurveyOrderItem>> GetAllAsync(
        SystemAdminSurveyOrderFilter filter,
        PagedRequest page,
        CancellationToken cancellationToken = default);

    Task<PagedResult<TenantOwnerSurveyOrderItem>> GetTenantOwnerPageAsync(
        Guid tenantId,
        SurveyOrderStatus? status,
        PagedRequest page,
        CancellationToken cancellationToken = default);

    Task<TenantOwnerSurveyOrderItem?> GetTenantOwnerByIdAsync(
        Guid tenantId,
        Guid surveyOrderId,
        CancellationToken cancellationToken = default);
}

public sealed record SystemManagerSurveyOrderFilter(
    SurveyOrderStatus? Status = null);

public sealed record SystemAdminSurveyOrderFilter(
    SurveyOrderStatus? Status = null,
    Guid? TenantId = null,
    Guid? FarmId = null);

public sealed record SystemManagerSurveyOrderItem(
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

public sealed record TenantOwnerSurveyOrderItem(
    Guid SurveyOrderId,
    string OrderNumber,
    Guid FarmId,
    string FarmName,
    SurveyOrderStatus Status,
    uint OrderVersion,
    decimal DepositAmount,
    decimal? TotalAmount,
    decimal? RemainingAmount,
    string Currency,
    TenantOwnerCurrentAppointmentItem? CurrentAppointment);

public sealed record TenantOwnerCurrentAppointmentItem(
    Guid AppointmentId,
    SurveyAppointmentPurpose Purpose,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    SurveyAppointmentStatus Status,
    uint AppointmentVersion);
