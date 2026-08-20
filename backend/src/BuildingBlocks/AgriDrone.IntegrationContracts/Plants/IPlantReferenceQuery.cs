namespace AgriDrone.IntegrationContracts.Plants;

public interface IPlantReferenceQuery
{
    Task<IReadOnlyList<PlantReferenceV1>> GetActiveByZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        CancellationToken cancellationToken = default);
}
