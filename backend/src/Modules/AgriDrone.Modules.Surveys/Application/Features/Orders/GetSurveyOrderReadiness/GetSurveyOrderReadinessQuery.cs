using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderReadiness;

public sealed record GetSurveyOrderReadinessQuery(
    Guid SurveyOrderId,
    Guid MissionId)
    : IRequest<Result<SurveyOrderReadinessResponse>>;
