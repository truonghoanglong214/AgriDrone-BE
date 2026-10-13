using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.MarkSurveyServiceExperimental;

public sealed record MarkSurveyServiceExperimentalCommand(
    Guid SurveyServiceId,
    string Reason,
    uint ExpectedVersion)
    : IRequest<Result<MarkSurveyServiceExperimentalResponse>>;
