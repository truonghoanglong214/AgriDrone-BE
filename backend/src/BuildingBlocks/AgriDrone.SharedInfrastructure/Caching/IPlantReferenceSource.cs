using AgriDrone.IntegrationContracts.Plants;

namespace AgriDrone.SharedInfrastructure.Caching;

public interface IPlantReferenceSource
{
    Task<IReadOnlyList<PlantReferenceV1>> LoadActiveByZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        CancellationToken cancellationToken = default);
}
