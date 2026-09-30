namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

internal interface ISurveyCatalogueQueries
{
    Task<IReadOnlyList<SurveyServiceCatalogueRecord>> GetServicesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyServicePriceRecord>> GetPriceHistoryAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default);
}
