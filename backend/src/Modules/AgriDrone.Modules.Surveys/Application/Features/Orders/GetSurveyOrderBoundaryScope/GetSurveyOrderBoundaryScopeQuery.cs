using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderBoundaryScope;

public sealed record GetSurveyOrderBoundaryScopeQuery(Guid SurveyOrderId)
    : IRequest<Result<GetSurveyOrderBoundaryScopeResponse>>;
