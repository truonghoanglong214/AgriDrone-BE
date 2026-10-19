namespace AgriDrone.IntegrationContracts.Farms;

public interface IMissionArchiveReferenceQuery
{
    Task<int> CountActiveForZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);

    Task<int> CountRetentionHistoryForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}

public interface ISurveyOrderArchiveReferenceQuery
{
    Task<int> CountActiveForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);

    Task<int> CountPublishedResultsForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);

    Task<int> CountRetentionHistoryForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}

