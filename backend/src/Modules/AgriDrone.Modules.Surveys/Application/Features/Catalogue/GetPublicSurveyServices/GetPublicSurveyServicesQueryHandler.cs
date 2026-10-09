using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetPublicSurveyServices;

internal sealed class GetPublicSurveyServicesQueryHandler(
    ISurveyCatalogueQueries catalogueQueries,
    TimeProvider timeProvider)
    : IRequestHandler<
        GetPublicSurveyServicesQuery,
        Result<IReadOnlyList<PublicSurveyServiceResponse>>>
{
    public async Task<Result<IReadOnlyList<PublicSurveyServiceResponse>>> Handle(
        GetPublicSurveyServicesQuery request,
        CancellationToken cancellationToken)
    {
        var evaluatedAt = timeProvider.GetUtcNow();
        var catalogue = await catalogueQueries.GetPublicCatalogueAsync(
            evaluatedAt,
            cancellationToken);

        IReadOnlyList<PublicSurveyServiceResponse> response = catalogue
            .Select(MapResponse)
            .ToArray();

        return Result.Success(response);
    }

    private static PublicSurveyServiceResponse MapResponse(
        PublicSurveyServiceCatalogueItem service) =>
        new(
            service.SurveyServiceId,
            service.Code,
            service.Name,
            service.Description,
            MapServiceType(service.ServiceType),
            service.Status == SurveyServiceStatus.Experimental,
            new PublicIndicativePriceResponse(
                service.PricePerPole,
                service.Currency,
                service.EffectiveFrom,
                service.EffectiveTo));

    private static PublicSurveyServiceType MapServiceType(
        SurveyServiceType serviceType) =>
        serviceType switch
        {
            SurveyServiceType.PlantHealth =>
                PublicSurveyServiceType.PlantHealth,
            SurveyServiceType.HarvestReadiness =>
                PublicSurveyServiceType.HarvestReadiness,
            _ => throw new ArgumentOutOfRangeException(
                nameof(serviceType),
                serviceType,
                "Unsupported public survey service type.")
        };
}
