using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetTenantOwnerSurveyOrder;

public sealed record GetTenantOwnerSurveyOrderQuery(Guid SurveyOrderId)
    : IRequest<Result<TenantOwnerSurveyOrderItem>>;
