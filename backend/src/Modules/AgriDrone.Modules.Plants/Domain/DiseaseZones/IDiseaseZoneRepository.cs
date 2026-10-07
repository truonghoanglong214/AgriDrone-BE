namespace AgriDrone.Modules.Plants.Domain.DiseaseZones;

internal interface IDiseaseZoneRepository
{
    Task<DiseaseZone?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid diseaseZoneId,
        CancellationToken cancellationToken = default);

    Task<DiseaseZone?> GetCurrentPublishedAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneKey,
        CancellationToken cancellationToken = default);

    void Add(DiseaseZone diseaseZone);
    void Update(DiseaseZone diseaseZone);
}
