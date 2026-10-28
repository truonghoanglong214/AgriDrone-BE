using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetTenantOwnerSurveyOrder;

internal sealed class GetTenantOwnerSurveyOrderQueryHandler(
    ISystemManagerSurveyOrderQueries queries,
    IExecutionContext executionContext)
    : IRequestHandler<GetTenantOwnerSurveyOrderQuery,
        Result<TenantOwnerSurveyOrderItem>>
{
    public async Task<Result<TenantOwnerSurveyOrderItem>> Handle(
        GetTenantOwnerSurveyOrderQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Result.Failure<TenantOwnerSurveyOrderItem>(
                SurveyOrderError.Forbidden());

        var order = await queries.GetTenantOwnerByIdAsync(
            tenantId,
            request.SurveyOrderId,
            cancellationToken);
        return order is null
            ? Result.Failure<TenantOwnerSurveyOrderItem>(
                SurveyOrderError.NotFound(request.SurveyOrderId))
            : Result.Success(order);
    }
}
