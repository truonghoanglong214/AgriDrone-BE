namespace AgriDrone.Modules.Plants.Domain.Plants;

public interface IPlantProfileRepository
{
    Task<PlantProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlantProfile?> GetByPoleIdentifierAsync(Guid farmId, string poleIdentifier, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlantProfile>> GetActiveProfilesByFarmAsync(Guid farmId, CancellationToken cancellationToken = default);
    Task AddAsync(PlantProfile profile, CancellationToken cancellationToken = default);
    Task UpdateAsync(PlantProfile profile, CancellationToken cancellationToken = default);
}
