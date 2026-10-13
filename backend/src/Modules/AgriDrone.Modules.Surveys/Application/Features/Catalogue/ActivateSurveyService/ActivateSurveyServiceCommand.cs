using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ActivateSurveyService;

public sealed record ActivateSurveyServiceCommand(
    Guid SurveyServiceId,
    string Reason,
    uint ExpectedVersion)
    : IRequest<Result<ActivateSurveyServiceResponse>>;
