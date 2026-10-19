using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetAssignedSurveyOrders;

internal sealed class GetAssignedSurveyOrdersQueryHandler(
    ISystemManagerSurveyOrderQueries queries,
    IExecutionContext executionContext)
    : IRequestHandler<GetAssignedSurveyOrdersQuery,
        Result<PagedResult<AssignedSurveyOrderResponse>>>
{
    public async Task<Result<PagedResult<AssignedSurveyOrderResponse>>> Handle(
        GetAssignedSurveyOrdersQuery request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<PagedResult<AssignedSurveyOrderResponse>>(
                SurveyOrderError.Forbidden());
        }

        var page = await queries.GetAssignedAsync(
            actorId,
            new SystemManagerSurveyOrderFilter(request.Status),
            new PagedRequest(request.PageNumber, request.PageSize),
            cancellationToken);

        if (page is null)
        {
            return Result.Failure<PagedResult<AssignedSurveyOrderResponse>>(
                SurveyOrderError.Forbidden());
        }

        var items = page.Items.Select(item => new AssignedSurveyOrderResponse(
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

        return Result.Success(new PagedResult<AssignedSurveyOrderResponse>(
            items,
            page.PageNumber,
            page.PageSize,
            page.TotalCount));
    }
}
