using AgriDrone.Modules.Plants.Domain.Mapping;

namespace AgriDrone.Modules.Plants.Domain.Plants;

internal interface IPlantRepository
{
    Task<Plant?> GetByIdAsync(
        Guid farmId,
        Guid zoneId,
        Guid plantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Plant>> GetByIdsAsync(
        Guid farmId,
        Guid zoneId,
        IReadOnlyCollection<Guid> plantIds,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(
        Guid farmId,
        string plantCode,
        CancellationToken cancellationToken = default);

    Task<bool> GridPositionExistsAsync(
        Guid zoneId,
        int rowIndex,
        int columnIndex,
        CancellationToken cancellationToken = default);

    void Add(Plant plant);

    void AddChangeEvent(PlantChangeEvent changeEvent);
}
