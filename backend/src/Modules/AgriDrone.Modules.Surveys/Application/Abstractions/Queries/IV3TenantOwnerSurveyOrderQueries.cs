using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

public interface IV3TenantOwnerSurveyOrderQueries
{
    Task<PagedResult<V3TenantOwnerSurveyOrderItem>> GetPageAsync(
        Guid tenantId,
        SurveyOrderStatus? status,
        PagedRequest page,
        CancellationToken cancellationToken);

    Task<V3TenantOwnerSurveyOrderItem?> GetByIdAsync(
        Guid tenantId,
        Guid surveyOrderId,
        CancellationToken cancellationToken);
}

public sealed record V3TenantOwnerSurveyOrderItem(
    Guid SurveyOrderId,
    string OrderNumber,
    Guid FarmId,
    string FarmName,
    string ServiceCode,
    string ServiceName,
    SurveyOrderStatus Status,
    uint OrderVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    TenantOwnerCurrentAppointmentItem? CurrentAppointment);
