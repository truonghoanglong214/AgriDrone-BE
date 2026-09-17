using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Plants.Domain.Conditions
{
    internal interface IPlantConditionRepository
    {
        Task<PlantCondition?> GetByIdAsync(
            Guid conditionId,
            CancellationToken cancellationToken = default);

        Task<bool> CodeExistsAsync(
            string code,
            CancellationToken cancellationToken = default);

        void Add(PlantCondition condition);

        void Update(PlantCondition condition);
    }
}
