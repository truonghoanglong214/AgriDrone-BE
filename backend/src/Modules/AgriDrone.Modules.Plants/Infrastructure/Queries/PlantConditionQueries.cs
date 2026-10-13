using AgriDrone.Modules.Plants.Application.Abstractions.Queries;
using AgriDrone.Modules.Plants.Application.Features.GetActivePlantConditions;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Queries;

internal sealed class PlantConditionQueries(
    PlantsDbContext context) : IPlantConditionQueries
{
    public async Task<IReadOnlyList<PlantConditionCatalogResponse>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.PlantConditions
            .AsNoTracking()
            .Where(condition => condition.IsActive)
            .OrderBy(condition => condition.ConditionType)
            .ThenBy(condition => condition.Name)
            .Select(condition => new PlantConditionCatalogResponse(
                condition.Id,
                condition.Code,
                condition.Name,
                condition.ScientificName,
                condition.ConditionType,
                condition.Description,
                condition.RevisionNumber))
            .ToListAsync(cancellationToken);
    }
}
