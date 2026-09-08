using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.FieldTasks.Domain.FieldTasks;
using AgriDrone.Modules.FieldTasks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.FieldTasks.Infrastructure.Queries;

internal sealed class FieldTaskArchiveReferenceQuery(
    FieldTasksDbContext dbContext)
    : IFieldTaskArchiveReferenceQuery
{
    public Task<int> CountOpenForZoneReferencesAsync(
        Guid farmId,
        IReadOnlyCollection<Guid> plantIds,
        IReadOnlyCollection<Guid> scanIds,
        CancellationToken cancellationToken = default)
    {
        if (plantIds.Count == 0 && scanIds.Count == 0)
        {
            return Task.FromResult(0);
        }

        var plantIdValues = plantIds.ToArray();
        var scanIdValues = scanIds.ToArray();

        return dbContext.FieldTasks
            .AsNoTracking()
            .CountAsync(
                task =>
                    task.FarmId == farmId &&
                    task.Status != FieldTaskStatus.Completed &&
                    task.Status != FieldTaskStatus.Cancelled &&
                    (task.PlantId.HasValue &&
                     plantIdValues.Contains(task.PlantId.Value) ||
                     task.SourceScanId.HasValue &&
                     scanIdValues.Contains(task.SourceScanId.Value)),
                cancellationToken);
    }

    public Task<int> CountOpenForFarmAsync(
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        dbContext.FieldTasks
            .AsNoTracking()
            .CountAsync(
                task =>
                    task.FarmId == farmId &&
                    task.Status != FieldTaskStatus.Completed &&
                    task.Status != FieldTaskStatus.Cancelled,
                cancellationToken);
}
