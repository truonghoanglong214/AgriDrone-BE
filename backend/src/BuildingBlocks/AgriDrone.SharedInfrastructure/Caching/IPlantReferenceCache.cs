using AgriDrone.IntegrationContracts.Plants;

namespace AgriDrone.SharedInfrastructure.Caching;

public interface IPlantReferenceCache
{
    Task<IReadOnlyList<PlantReferenceV1>?> TryGetAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        IReadOnlyList<PlantReferenceV1> references,
        CancellationToken cancellationToken = default);

    Task InvalidateZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default);
}
