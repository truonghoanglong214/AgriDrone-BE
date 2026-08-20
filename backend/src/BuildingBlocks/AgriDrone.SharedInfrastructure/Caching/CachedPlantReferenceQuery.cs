using AgriDrone.IntegrationContracts.Plants;

namespace AgriDrone.SharedInfrastructure.Caching;

internal sealed class CachedPlantReferenceQuery(
    IPlantReferenceCache cache,
    IPlantReferenceSource source) : IPlantReferenceQuery
{
    public async Task<IReadOnlyList<PlantReferenceV1>> GetActiveByZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        CancellationToken cancellationToken = default)
    {
        var cached = await cache.TryGetAsync(
            tenantId,
            farmId,
            zoneId,
            mapVersionId,
            cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var references = await source.LoadActiveByZoneAsync(
            tenantId,
            farmId,
            zoneId,
            mapVersionId,
            cancellationToken);
        await cache.SetAsync(
            tenantId,
            farmId,
            zoneId,
            mapVersionId,
            references,
            cancellationToken);
        return references;
    }
}
