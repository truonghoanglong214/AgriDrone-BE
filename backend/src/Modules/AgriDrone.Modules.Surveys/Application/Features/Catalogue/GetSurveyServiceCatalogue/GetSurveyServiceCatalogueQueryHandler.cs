using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetSurveyServiceCatalogue;

internal sealed class GetSurveyServiceCatalogueQueryHandler(
    ISurveyCatalogueQueries catalogueQueries,
    TimeProvider timeProvider)
    : IRequestHandler<
        GetSurveyServiceCatalogueQuery,
        Result<IReadOnlyList<SurveyServiceCatalogueResponse>>>
{
    public async Task<Result<IReadOnlyList<SurveyServiceCatalogueResponse>>> Handle(
        GetSurveyServiceCatalogueQuery request,
        CancellationToken cancellationToken)
    {
        var evaluatedAt = timeProvider.GetUtcNow();
        var catalogue = await catalogueQueries.GetCatalogueAsync(
            evaluatedAt,
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
                service.CurrentPrice is null
                    ? null
                    : new SurveyServiceCurrentPriceResponse(
                        service.CurrentPrice.PriceVersionId,
                        service.CurrentPrice.PricePerPole,
                        service.CurrentPrice.Currency,
                        service.CurrentPrice.EffectiveFrom,
                        service.CurrentPrice.EffectiveTo)))
            .ToArray();

        return Result.Success(response);
    }
}
