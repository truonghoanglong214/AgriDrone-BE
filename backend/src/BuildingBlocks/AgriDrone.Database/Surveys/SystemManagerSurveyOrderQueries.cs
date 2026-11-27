using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SystemManagerSurveyOrderQueries(
    AgriDroneSchemaDbContext context) : ISystemManagerSurveyOrderQueries
{
    private const int MaximumPageSize = 100;

    public async Task<PagedResult<SystemManagerSurveyOrderItem>?> GetAssignedAsync(
        Guid actorId,
        SystemManagerSurveyOrderFilter filter,
        PagedRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(actorId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(filter);
        EnsureValidPage(page);

        var managerProfile = await context.Set<SystemManagerProfile>()
            .AsNoTracking()
            .Where(profile =>
                profile.UserId == actorId &&
                profile.User.Status == UserStatus.Active &&
                profile.Status == SystemManagerProfileStatus.Active)
            .Select(profile => new { profile.Id })
            .SingleOrDefaultAsync(cancellationToken);

        if (managerProfile is null)
        {
            return null;
        }

        var assignments = context.Set<FarmManagerAssignment>()
            .AsNoTracking()
            .Where(assignment =>
                assignment.SystemManagerProfileId == managerProfile.Id &&
                assignment.EndedAt == null);

        var currentAppointments = SelectCurrentAppointments(context.Set<SurveyOrder>().AsNoTracking());
        var orders =
            from order in context.Set<SurveyOrder>().AsNoTracking()
            join assignment in assignments
                on new { order.TenantId, order.FarmId }
                equals new { assignment.TenantId, assignment.FarmId }
            join farm in context.Set<Farm>().AsNoTracking()
                on new { order.TenantId, order.FarmId }
                equals new { farm.TenantId, FarmId = farm.Id }
            join service in context.Set<SurveyService>().AsNoTracking()
                on order.SurveyServiceId equals service.Id
            join appointment in currentAppointments
                on order.Id equals appointment.SurveyOrderId into appointments
            from currentAppointment in appointments.DefaultIfEmpty()
            where farm.DeletedAt == null
            select new { Order = order, Farm = farm, Service = service, CurrentAppointment = currentAppointment };

        if (filter.Status.HasValue)
        {
            orders = orders.Where(row =>
                row.Order.Status == filter.Status.Value);
        }

        return await orders
            .OrderByDescending(row => row.Order.CreatedAt)
            .ThenByDescending(row => row.Order.Id)
            .Select(row => new SystemManagerSurveyOrderItem(
                row.Order.Id,
                row.Order.OrderNumber,
                row.Order.TenantId,
                row.Order.FarmId,
                row.Farm.Code,
                row.Farm.Name,
                row.Order.SurveyServiceId,
                row.Service.Code,
                row.Service.Name,
                row.Order.Status,
                row.Order.Version,
                row.Order.RequiresBaselineMapping,
                row.Order.FarmBoundaryVersionId,
                row.Order.BoundaryCaptureSessionId,
                row.Order.BaselineRoutePlanId,
                row.CurrentAppointment == null ? null : row.CurrentAppointment.Id,
                row.CurrentAppointment == null ? null : row.CurrentAppointment.Version,
                row.CurrentAppointment == null ? null : row.CurrentAppointment.Status,
                row.Order.CreatedAt,
                row.Order.UpdatedAt,
                row.CurrentAppointment == null ? null : row.CurrentAppointment.Purpose,
                row.CurrentAppointment == null ? null : row.CurrentAppointment.ProposedStartAt,
                row.CurrentAppointment == null ? null : row.CurrentAppointment.ProposedEndAt))
            .ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<PagedResult<SystemManagerSurveyOrderItem>> GetAllAsync(
        SystemAdminSurveyOrderFilter filter,
        PagedRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        EnsureValidPage(page);

        var orders =
            from order in context.Set<SurveyOrder>().AsNoTracking()
            join farm in context.Set<Farm>().AsNoTracking()
                on new { order.TenantId, order.FarmId }
                equals new { farm.TenantId, FarmId = farm.Id }
            join service in context.Set<SurveyService>().AsNoTracking()
                on order.SurveyServiceId equals service.Id
            where farm.DeletedAt == null
            select new { Order = order, Farm = farm, Service = service };

        if (filter.Status.HasValue)
        {
            orders = orders.Where(row =>
                row.Order.Status == filter.Status.Value);
        }

        if (filter.TenantId.HasValue)
        {
            orders = orders.Where(row =>
                row.Order.TenantId == filter.TenantId.Value);
        }

        if (filter.FarmId.HasValue)
        {
            orders = orders.Where(row =>
                row.Order.FarmId == filter.FarmId.Value);
        }

        return await orders
            .OrderByDescending(row => row.Order.CreatedAt)
            .ThenByDescending(row => row.Order.Id)
            .Select(row => new SystemManagerSurveyOrderItem(
                row.Order.Id,
                row.Order.OrderNumber,
                row.Order.TenantId,
                row.Order.FarmId,
                row.Farm.Code,
                row.Farm.Name,
                row.Order.SurveyServiceId,
                row.Service.Code,
                row.Service.Name,
                row.Order.Status,
                row.Order.Version,
                row.Order.RequiresBaselineMapping,
                row.Order.FarmBoundaryVersionId,
                row.Order.BoundaryCaptureSessionId,
                row.Order.BaselineRoutePlanId,
                row.Order.Appointments
                    .Where(item => item.Status == SurveyAppointmentStatus.Proposed ||
                                   item.Status == SurveyAppointmentStatus.Confirmed ||
                                   item.Status == SurveyAppointmentStatus.RescheduleRequested)
                    .OrderByDescending(item => item.Purpose == SurveyAppointmentPurpose.DiseaseCapture)
                    .ThenByDescending(item => item.RevisionNumber)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefault(),
                row.Order.Appointments
                    .Where(item => item.Status == SurveyAppointmentStatus.Proposed ||
                                   item.Status == SurveyAppointmentStatus.Confirmed ||
                                   item.Status == SurveyAppointmentStatus.RescheduleRequested)
                    .OrderByDescending(item => item.Purpose == SurveyAppointmentPurpose.DiseaseCapture)
                    .ThenByDescending(item => item.RevisionNumber)
                    .Select(item => (uint?)item.Version)
                    .FirstOrDefault(),
                row.Order.Appointments
                    .Where(item => item.Status == SurveyAppointmentStatus.Proposed ||
                                   item.Status == SurveyAppointmentStatus.Confirmed ||
                                   item.Status == SurveyAppointmentStatus.RescheduleRequested)
                    .OrderByDescending(item => item.Purpose == SurveyAppointmentPurpose.DiseaseCapture)
                    .ThenByDescending(item => item.RevisionNumber)
                    .Select(item => (SurveyAppointmentStatus?)item.Status)
                    .FirstOrDefault(),
                row.Order.CreatedAt,
                row.Order.UpdatedAt))
            .ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<PagedResult<TenantOwnerSurveyOrderItem>> GetTenantOwnerPageAsync(
        Guid tenantId,
        SurveyOrderStatus? status,
        PagedRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        EnsureValidPage(page);

        var orders = GetTenantOwnerOrders(tenantId, status: status);

        return await orders.ToPagedResultAsync(page, cancellationToken);
    }

    public Task<TenantOwnerSurveyOrderItem?> GetTenantOwnerByIdAsync(
        Guid tenantId,
        Guid surveyOrderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(surveyOrderId, Guid.Empty);

        return GetTenantOwnerOrders(tenantId, surveyOrderId: surveyOrderId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<TenantOwnerSurveyOrderItem> GetTenantOwnerOrders(
        Guid tenantId,
        Guid? surveyOrderId = null,
        SurveyOrderStatus? status = null) =>
        from order in context.Set<SurveyOrder>().AsNoTracking()
        join farm in context.Set<Farm>().AsNoTracking()
            on new { order.TenantId, order.FarmId }
            equals new { farm.TenantId, FarmId = farm.Id }
        join service in context.Set<SurveyService>().AsNoTracking()
            on order.SurveyServiceId equals service.Id
        join appointment in SelectCurrentAppointments(context.Set<SurveyOrder>().AsNoTracking())
            on order.Id equals appointment.SurveyOrderId into appointments
        from currentAppointment in appointments.DefaultIfEmpty()
        where order.TenantId == tenantId && farm.DeletedAt == null
            && order.Scope == ContractScope.Legacy && service.ServiceType.HasValue
            && (!surveyOrderId.HasValue || order.Id == surveyOrderId.Value)
            && (!status.HasValue || order.Status == status.Value)
        orderby order.CreatedAt descending, order.Id descending
        select new TenantOwnerSurveyOrderItem(
            order.Id,
            order.OrderNumber,
            order.FarmId,
            farm.Name,
            order.Status,
            order.Version,
            order.DepositAmount,
            order.FinalPrice,
            order.RemainingAmount,
            order.Currency ?? "VND",
            currentAppointment == null
                ? null
                : new TenantOwnerCurrentAppointmentItem(
                    currentAppointment.Id,
                    currentAppointment.Purpose,
                    currentAppointment.ProposedStartAt,
                    currentAppointment.ProposedEndAt,
                    currentAppointment.Status,
                    currentAppointment.Version),
            service.ServiceType!.Value,
            order.ConfirmedMappingPoleCount, order.ConfirmedPlantHealthPoleCount,
            order.BookedMappingPricePerPole, order.BookedPlantHealthPricePerPole,
            order.MappingSubtotal, order.PlantHealthSubtotal,
            order.ComboDiscountPercentage, order.ComboDiscountAmount);

    // The active-appointment index allows one row per order and purpose.
    internal static IQueryable<SurveyAppointment> SelectCurrentAppointments(IQueryable<SurveyOrder> orders) =>
        from order in orders
        from appointment in order.Appointments
        where appointment.Purpose == (order.Scope == ContractScope.V3
            ? (order.Appointments.Any(item => item.Purpose == SurveyAppointmentPurpose.DiseaseCapture)
                ? SurveyAppointmentPurpose.DiseaseCapture
                : SurveyAppointmentPurpose.Capture)
            : (!order.RequiresBaselineMapping ||
               (order.PostFlightSettlement ? order.FarmBaseMapVersionId.HasValue : order.PricingConfirmedAt.HasValue)
                ? SurveyAppointmentPurpose.PaidService
                : SurveyAppointmentPurpose.BaselineMapping))
            && (appointment.Status == SurveyAppointmentStatus.Proposed ||
                appointment.Status == SurveyAppointmentStatus.Confirmed ||
                appointment.Status == SurveyAppointmentStatus.RescheduleRequested)
        select appointment;

    private static void EnsureValidPage(PagedRequest page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.PageNumber < 1 ||
            page.PageSize < 1 ||
            page.PageSize > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                $"Page number must be positive and page size must be between 1 and {MaximumPageSize}.");
        }
    }
}
