using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetSurveyServiceCatalogue;

internal sealed class GetSurveyServiceCatalogueQueryHandler(
    ISurveyCatalogueQueries catalogueQueries)
    : IRequestHandler<
        GetSurveyServiceCatalogueQuery,
        Result<IReadOnlyList<SurveyServiceCatalogueResponse>>>
{
    public async Task<Result<IReadOnlyList<SurveyServiceCatalogueResponse>>> Handle(
        GetSurveyServiceCatalogueQuery request,
        CancellationToken cancellationToken)
    {
        var catalogue = await catalogueQueries.GetCatalogueAsync(
            cancellationToken);
        IReadOnlyList<SurveyServiceCatalogueResponse> response = catalogue
            .Select(service => new SurveyServiceCatalogueResponse(
                service.SurveyServiceId,
                service.Code,
                service.Name,
                service.Description,
                service.ServiceType,
                service.Status,
                service.Version,
                service.CreatedAt,
                service.UpdatedAt,
                service.Prices.Select(price =>
                        new SurveyServicePriceCatalogueResponse(
                            price.PriceVersionId,
                            price.PricePerPole,
                            price.LegacyPricePerHa,
                            price.Currency,
                            price.EffectiveFrom,
                            price.EffectiveTo,
                            price.CreatedBy,
                            price.CreatedAt,
                            price.IsLegacyPerHectarePrice))
                    .ToArray()))
            .ToArray();

        return Result.Success(response);
    }
}
