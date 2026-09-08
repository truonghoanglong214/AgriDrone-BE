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

public interface IFieldTaskArchiveReferenceQuery
{
    Task<int> CountOpenForZoneReferencesAsync(
        Guid farmId,
        IReadOnlyCollection<Guid> plantIds,
        IReadOnlyCollection<Guid> scanIds,
        CancellationToken cancellationToken = default);

    Task<int> CountOpenForFarmAsync(
        Guid farmId,
        CancellationToken cancellationToken = default);
}

public interface IPlantArchiveReferenceQuery
{
    Task<ZonePlantReferences> GetZoneReferencesAsync(
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default);
}

public sealed record ZonePlantReferences(
    IReadOnlyCollection<Guid> PlantIds,
    IReadOnlyCollection<Guid> ScanIds);
