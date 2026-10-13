using AgriDrone.SharedKernel.Application;
using AgriDrone.Modules.Surveys.Domain;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderReadiness;

public sealed record GetSurveyOrderReadinessQuery(
    Guid SurveyOrderId,
    Guid MissionId,
    SurveyOperationPurpose Purpose)
    : IRequest<Result<SurveyOrderReadinessResponse>>;
