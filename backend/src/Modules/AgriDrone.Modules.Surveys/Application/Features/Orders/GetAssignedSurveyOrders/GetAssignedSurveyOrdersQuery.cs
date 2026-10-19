using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetAssignedSurveyOrders;

public sealed record GetAssignedSurveyOrdersQuery(
    SurveyOrderStatus? Status,
    int PageNumber = 1,
    int PageSize = 20)
    : IRequest<Result<PagedResult<AssignedSurveyOrderResponse>>>;
