using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Queries;

internal sealed class FarmAssignmentReferenceQuery(
    FarmsDbContext dbContext) : IFarmAssignmentReferenceQuery
{
    public Task<bool> IsActiveFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        dbContext.Farms
            .AsNoTracking()
            .AnyAsync(
                farm =>
                    farm.Id == farmId &&
                    farm.TenantId == tenantId &&
                    farm.Status == GeneralStatus.Active &&
                    farm.DeletedAt == null,
                cancellationToken);
}
