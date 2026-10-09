using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.UpdateSurveyServiceMetadata;

public sealed record UpdateSurveyServiceMetadataCommand(
    Guid SurveyServiceId,
    string Name,
    string Description,
    uint ExpectedVersion)
    : IRequest<Result<UpdateSurveyServiceMetadataResponse>>;
