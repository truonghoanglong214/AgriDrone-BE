using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServicePrice;

public sealed record CreateSurveyServicePriceCommand(
    Guid SurveyServiceId,
    decimal AmountPerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    uint ExpectedServiceVersion)
    : IRequest<Result<CreateSurveyServicePriceResponse>>;
