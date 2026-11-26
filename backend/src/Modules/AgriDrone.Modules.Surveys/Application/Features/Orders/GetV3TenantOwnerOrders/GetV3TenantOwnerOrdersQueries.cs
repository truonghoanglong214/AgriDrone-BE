using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using FluentValidation;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetV3TenantOwnerOrders;

public sealed record GetV3TenantOwnerOrdersQuery(
    int PageNumber,
    int PageSize,
    SurveyOrderStatus? Status)
    : IRequest<Result<PagedResult<V3TenantOwnerSurveyOrderItem>>>;

public sealed record GetV3TenantOwnerOrderQuery(Guid SurveyOrderId)
    : IRequest<Result<V3TenantOwnerSurveyOrderItem>>;

internal sealed class GetV3TenantOwnerOrdersQueryValidator : AbstractValidator<GetV3TenantOwnerOrdersQuery>
{
    public GetV3TenantOwnerOrdersQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query)
            .Must(query => (query.PageNumber - 1L) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page exceeds the supported offset.");
        RuleFor(query => query.Status)
            .Must(status => !status.HasValue || SurveyOrder.IsV3Status(status.Value))
            .WithMessage("Status is not supported for V3 orders.");
    }
}

internal sealed class GetV3TenantOwnerOrderQueryValidator : AbstractValidator<GetV3TenantOwnerOrderQuery>
{
    public GetV3TenantOwnerOrderQueryValidator()
    {
        RuleFor(query => query.SurveyOrderId).NotEmpty();
    }
}

internal sealed class GetV3TenantOwnerOrdersQueryHandler(
    IV3TenantOwnerSurveyOrderQueries queries,
    IExecutionContext executionContext)
    : IRequestHandler<GetV3TenantOwnerOrdersQuery, Result<PagedResult<V3TenantOwnerSurveyOrderItem>>>,
      IRequestHandler<GetV3TenantOwnerOrderQuery, Result<V3TenantOwnerSurveyOrderItem>>
{
    public async Task<Result<PagedResult<V3TenantOwnerSurveyOrderItem>>> Handle(
        GetV3TenantOwnerOrdersQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return Result.Failure<PagedResult<V3TenantOwnerSurveyOrderItem>>(SurveyOrderError.Forbidden());

        var page = await queries.GetPageAsync(
            tenantId,
            request.Status,
            new PagedRequest(request.PageNumber, request.PageSize),
            cancellationToken);
        return Result.Success(page);
    }

    public async Task<Result<V3TenantOwnerSurveyOrderItem>> Handle(
        GetV3TenantOwnerOrderQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return Result.Failure<V3TenantOwnerSurveyOrderItem>(SurveyOrderError.Forbidden());

        var order = await queries.GetByIdAsync(tenantId, request.SurveyOrderId, cancellationToken);
        return order is null
            ? Result.Failure<V3TenantOwnerSurveyOrderItem>(SurveyOrderError.NotFound(request.SurveyOrderId))
            : Result.Success(order);
    }
}
