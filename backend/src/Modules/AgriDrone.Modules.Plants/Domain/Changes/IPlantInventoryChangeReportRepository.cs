namespace AgriDrone.Modules.Plants.Domain.Changes;

public interface IPlantInventoryChangeReportRepository
{
    Task<PlantInventoryChangeReport?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<PlantInventoryChangeReport?> GetByIdempotencyAsync(
        string callerScope,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<bool> HasOpenReportForPlantAsync(
        Guid farmId,
        Guid plantId,
        CancellationToken cancellationToken = default);

    Task<bool> HasOpenReportAtPoleAsync(
        Guid farmId,
        string poleLocationKey,
        CancellationToken cancellationToken = default);

    void Add(PlantInventoryChangeReport report);

    void Update(PlantInventoryChangeReport report);
}
