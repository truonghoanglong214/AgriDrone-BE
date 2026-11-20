using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Repositories;

internal sealed class PlantProfileRepository(PlantsDbContext context)
    : IPlantProfileRepository
{
    public Task<PlantProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return context.PlantProfiles.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<PlantProfile?> GetByPoleIdentifierAsync(Guid farmId, string poleIdentifier, CancellationToken cancellationToken = default)
    {
        var normalized = poleIdentifier?.Trim().ToUpperInvariant();
        return context.PlantProfiles.SingleOrDefaultAsync(
            p => p.FarmId == farmId &&
                 p.PhysicalPoleIdentifier == normalized &&
                 p.Status == PlantProfileStatus.Active,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PlantProfile>> GetActiveProfilesByFarmAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await context.PlantProfiles
            .Where(p => p.FarmId == farmId && p.Status == PlantProfileStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PlantProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await context.PlantProfiles.AddAsync(profile, cancellationToken);
    }

    public Task UpdateAsync(PlantProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        context.PlantProfiles.Update(profile);
        return Task.CompletedTask;
    }
}
