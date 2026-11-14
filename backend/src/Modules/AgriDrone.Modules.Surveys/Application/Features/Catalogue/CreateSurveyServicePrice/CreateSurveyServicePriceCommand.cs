using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServicePrice;

public sealed record CreateSurveyServicePriceCommand(
    Guid SurveyServiceId,
    decimal AmountPerPole,
    string Currency,
    uint ExpectedServiceVersion,
    decimal? MappingPricePerPole = null,
    decimal? PlantHealthPricePerPole = null)
    : IRequest<Result<CreateSurveyServicePriceResponse>>;
