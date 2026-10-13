using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetSurveyServicePriceHistory;

internal sealed class GetSurveyServicePriceHistoryQueryHandler(
    ISurveyServiceRepository repository,
    ISurveyCatalogueQueries catalogueQueries)
    : IRequestHandler<
        GetSurveyServicePriceHistoryQuery,
        Result<IReadOnlyList<SurveyServicePriceHistoryResponse>>>
{
    public async Task<Result<IReadOnlyList<SurveyServicePriceHistoryResponse>>> Handle(
        GetSurveyServicePriceHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var service = await repository.GetByIdAsync(
            request.SurveyServiceId,
            cancellationToken);
        if (service is null)
        {
            return Result.Failure<IReadOnlyList<SurveyServicePriceHistoryResponse>>(
                SurveyServiceError.NotFound());
        }

        var prices = await catalogueQueries.GetPriceHistoryAsync(
            service.Id,
            cancellationToken);
        IReadOnlyList<SurveyServicePriceHistoryResponse> response = prices
            .Select(price => new SurveyServicePriceHistoryResponse(
                price.PriceVersionId,
                price.SurveyServiceId,
                price.PricePerPole,
                price.LegacyPricePerHa,
                price.Currency,
                price.EffectiveFrom,
                price.EffectiveTo,
                price.CreatedBy,
                price.CreatedAt,
                price.IsLegacyPerHectarePrice))
            .ToArray();
        return Result.Success(response);
    }
}
