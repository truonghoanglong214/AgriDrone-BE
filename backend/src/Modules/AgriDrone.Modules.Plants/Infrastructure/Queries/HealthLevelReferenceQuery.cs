using AgriDrone.IntegrationContracts.Plants;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Queries;

internal sealed class HealthLevelReferenceQuery(
    PlantsDbContext dbContext) : IHealthLevelReferenceQuery
{
    private const string UnknownHealthCode = "UNKNOWN";

    public Task<Guid?> GetActiveUnknownIdAsync(
        CancellationToken cancellationToken = default) =>
        dbContext.HealthLevels
            .AsNoTracking()
            .Where(level =>
                level.Code == UnknownHealthCode && level.IsActive)
            .Select(level => (Guid?)level.Id)
            .SingleOrDefaultAsync(cancellationToken);
}
