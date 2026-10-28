using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetTenantOwnerSurveyOrders;

internal sealed class GetTenantOwnerSurveyOrdersQueryHandler(
    ISystemManagerSurveyOrderQueries queries,
    IExecutionContext executionContext)
    : IRequestHandler<GetTenantOwnerSurveyOrdersQuery,
        Result<PagedResult<TenantOwnerSurveyOrderItem>>>
{
    public async Task<Result<PagedResult<TenantOwnerSurveyOrderItem>>> Handle(
        GetTenantOwnerSurveyOrdersQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Result.Failure<PagedResult<TenantOwnerSurveyOrderItem>>(
                SurveyOrderError.Forbidden());

        var page = await queries.GetTenantOwnerPageAsync(
            tenantId,
            request.Status,
            new PagedRequest(request.PageNumber, request.PageSize),
            cancellationToken);
        return Result.Success(page);
    }
}
