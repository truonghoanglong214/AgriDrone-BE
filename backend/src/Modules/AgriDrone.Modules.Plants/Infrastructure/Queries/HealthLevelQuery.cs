using AgriDrone.Modules.Plants.Application.Abstractions.Queries;
using AgriDrone.Modules.Plants.Application.Features.GetHealthLevels;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Plants.Infrastructure.Queries
{
    internal sealed class HealthLevelQuery(
        PlantsDbContext context) : IHealthLevelQueries
    {
        public async Task<IReadOnlyList<HealthLevelResponse>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return await context.HealthLevels
            .AsNoTracking()
            .Where(level => level.IsActive)
            .OrderBy(level => level.Rank ?? -1)
            .Select(level => new HealthLevelResponse(
                level.Id,
                level.Code,
                level.Name,
                level.Rank,
                level.IsHealthy,
                level.Description))
            .ToListAsync(cancellationToken);
        }
    }
}
