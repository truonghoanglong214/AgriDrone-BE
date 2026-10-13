using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Repositories;

internal sealed class FarmBoundaryRepository(FarmsDbContext context)
    : IFarmBoundaryRepository
{
    public Task<FarmBoundary?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid boundaryId,
        CancellationToken cancellationToken = default)
        => context.FarmBoundaries.SingleOrDefaultAsync(
            boundary => boundary.TenantId == tenantId &&
                        boundary.FarmId == farmId &&
                        boundary.Id == boundaryId,
            cancellationToken);

    public Task<FarmBoundary?> GetCurrentApprovedAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default)
        => context.FarmBoundaries.SingleOrDefaultAsync(
            boundary => boundary.TenantId == tenantId &&
                        boundary.FarmId == farmId &&
                        boundary.Status == FarmBoundaryStatus.Approved,
            cancellationToken);

    public async Task<int> GetNextVersionNumberAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        var current = await context.FarmBoundaries
            .Where(boundary => boundary.TenantId == tenantId && boundary.FarmId == farmId)
            .MaxAsync(boundary => (int?)boundary.VersionNumber, cancellationToken);

        return (current ?? 0) + 1;
    }

    public void Add(FarmBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        context.FarmBoundaries.Add(boundary);
    }

    public void Update(FarmBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        context.FarmBoundaries.Update(boundary);
    }
}
