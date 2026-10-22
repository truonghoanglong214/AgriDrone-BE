using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSystemAdminSurveyOrders;

public sealed record GetSystemAdminSurveyOrdersQuery(
    SurveyOrderStatus? Status = null,
    Guid? TenantId = null,
    Guid? FarmId = null,
    int PageNumber = 1,
    int PageSize = 20)
    : IRequest<Result<PagedResult<SystemAdminSurveyOrderResponse>>>;
