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
}
