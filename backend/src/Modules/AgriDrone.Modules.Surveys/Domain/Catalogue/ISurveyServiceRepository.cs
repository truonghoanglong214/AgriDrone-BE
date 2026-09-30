namespace AgriDrone.Modules.Surveys.Domain;

internal interface ISurveyServiceRepository
{
    Task<SurveyService?> GetByIdAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default);

    Task<SurveyService?> GetByIdWithPricesAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default);
}
