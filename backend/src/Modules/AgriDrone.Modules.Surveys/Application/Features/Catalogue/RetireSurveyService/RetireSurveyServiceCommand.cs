using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.RetireSurveyService;

public sealed record RetireSurveyServiceCommand(
    Guid SurveyServiceId,
    string Reason,
    uint ExpectedVersion)
    : IRequest<Result<RetireSurveyServiceResponse>>;
