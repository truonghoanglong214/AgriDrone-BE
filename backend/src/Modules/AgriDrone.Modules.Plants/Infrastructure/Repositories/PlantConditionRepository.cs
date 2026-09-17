using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Plants.Infrastructure.Repositories
{
    internal sealed class PlantConditionRepository(
        PlantsDbContext context) : IPlantConditionRepository
    {
        public void Add(PlantCondition condition)
        {
            ArgumentNullException.ThrowIfNull(condition);
            context.PlantConditions.Add(condition);
        }

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            return context.PlantConditions
                .AsNoTracking()
                .AnyAsync(condition => condition.Code == normalizedCode, cancellationToken);
        }

        public Task<PlantCondition?> GetByIdAsync(Guid conditionId, CancellationToken cancellationToken = default)
        {
            return  context.PlantConditions
                .AsNoTracking()
                .SingleOrDefaultAsync(condition => condition.Id == conditionId, cancellationToken);
        }

        public void Update(PlantCondition condition)
        {
            ArgumentNullException.ThrowIfNull(condition);
            context.PlantConditions.Update(condition);
        }
    }
}
