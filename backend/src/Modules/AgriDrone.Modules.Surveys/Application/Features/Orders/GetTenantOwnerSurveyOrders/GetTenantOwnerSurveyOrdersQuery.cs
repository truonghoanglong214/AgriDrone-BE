using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetTenantOwnerSurveyOrders;

public sealed record GetTenantOwnerSurveyOrdersQuery(
    int PageNumber,
    int PageSize,
    SurveyOrderStatus? Status)
    : IRequest<Result<PagedResult<TenantOwnerSurveyOrderItem>>>;
