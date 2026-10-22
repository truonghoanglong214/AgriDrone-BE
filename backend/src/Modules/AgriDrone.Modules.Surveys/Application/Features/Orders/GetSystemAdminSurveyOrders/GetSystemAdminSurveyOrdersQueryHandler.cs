using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSystemAdminSurveyOrders;

internal sealed class GetSystemAdminSurveyOrdersQueryHandler(
    ISystemManagerSurveyOrderQueries queries)
    : IRequestHandler<GetSystemAdminSurveyOrdersQuery,
        Result<PagedResult<SystemAdminSurveyOrderResponse>>>
{
    public async Task<Result<PagedResult<SystemAdminSurveyOrderResponse>>> Handle(
        GetSystemAdminSurveyOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var filter = new SystemAdminSurveyOrderFilter(
            request.Status,
            request.TenantId,
            request.FarmId);

        var page = await queries.GetAllAsync(
            filter,
            new PagedRequest(request.PageNumber, request.PageSize),
            cancellationToken);

        var items = page.Items.Select(item => new SystemAdminSurveyOrderResponse(
                item.SurveyOrderId,
                item.OrderNumber,
                item.TenantId,
                item.FarmId,
                item.FarmCode,
                item.FarmName,
                item.SurveyServiceId,
                item.SurveyServiceCode,
                item.SurveyServiceName,
                item.Status,
                item.OrderVersion,
                item.RequiresBaselineMapping,
                item.FarmBoundaryVersionId,
                item.BoundaryCaptureSessionId,
                item.BaselineRoutePlanId,
                item.CurrentAppointmentId,
                item.CurrentAppointmentVersion,
                item.CurrentAppointmentStatus,
                item.CreatedAt,
                item.UpdatedAt))
            .ToArray();

        return Result.Success(new PagedResult<SystemAdminSurveyOrderResponse>(
            items,
            page.PageNumber,
            page.PageSize,
            page.TotalCount));
    }
}
