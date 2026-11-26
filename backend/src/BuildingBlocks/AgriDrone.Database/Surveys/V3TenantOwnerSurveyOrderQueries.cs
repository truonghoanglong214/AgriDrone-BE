using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class V3TenantOwnerSurveyOrderQueries(AgriDroneSchemaDbContext context)
    : IV3TenantOwnerSurveyOrderQueries
{
    public Task<PagedResult<V3TenantOwnerSurveyOrderItem>> GetPageAsync(
        Guid tenantId,
        SurveyOrderStatus? status,
        PagedRequest page,
        CancellationToken cancellationToken) =>
        SelectOrders(tenantId, status: status).ToPagedResultAsync(page, cancellationToken);

    public Task<V3TenantOwnerSurveyOrderItem?> GetByIdAsync(
        Guid tenantId,
        Guid surveyOrderId,
        CancellationToken cancellationToken) =>
        SelectOrders(tenantId, surveyOrderId: surveyOrderId).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<V3TenantOwnerSurveyOrderItem> SelectOrders(
        Guid tenantId,
        Guid? surveyOrderId = null,
        SurveyOrderStatus? status = null)
    {
        var scopedOrders = context.Set<SurveyOrder>().AsNoTracking()
            .Where(order => order.TenantId == tenantId && order.Scope == ContractScope.V3
                && (!surveyOrderId.HasValue || order.Id == surveyOrderId.Value)
                && (!status.HasValue || order.Status == status.Value));
        var currentAppointments = SystemManagerSurveyOrderQueries.SelectCurrentAppointments(scopedOrders);

        return from order in scopedOrders
               join farm in context.Set<Farm>() on new { order.TenantId, order.FarmId }
                   equals new { farm.TenantId, FarmId = farm.Id }
               join service in context.Set<SurveyService>() on order.SurveyServiceId equals service.Id
               join appointment in currentAppointments on order.Id equals appointment.SurveyOrderId into appointments
               from currentAppointment in appointments.DefaultIfEmpty()
               where farm.DeletedAt == null && service.Scope == ContractScope.V3
               orderby order.CreatedAt descending, order.Id descending
               select new V3TenantOwnerSurveyOrderItem(
                   order.Id,
                   order.OrderNumber,
                   farm.Id,
                   farm.Name,
                   service.Code,
                   service.Name,
                   order.Status,
                   order.Version,
                   order.CreatedAt,
                   order.UpdatedAt,
                   currentAppointment == null
                       ? null
                       : new TenantOwnerCurrentAppointmentItem(
                           currentAppointment.Id,
                           currentAppointment.Purpose,
                           currentAppointment.ProposedStartAt,
                           currentAppointment.ProposedEndAt,
                           currentAppointment.Status,
                           currentAppointment.Version));
    }
}
