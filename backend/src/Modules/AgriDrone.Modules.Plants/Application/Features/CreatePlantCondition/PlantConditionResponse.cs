using AgriDrone.Modules.Plants.Domain.Conditions;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition
{
    public sealed record PlantConditionResponse(
        Guid Id,
        string Code,
        string Name,
        string? ScientificName,
        ConditionType ConditionType,
        string? Description,
        int RevisionNumber,
        bool IsActive,
        DateTimeOffset CreatedAt,
        DateTimeOffset? RetiredAt,
        long Version)
    {
        public static PlantConditionResponse From(PlantCondition condition)
        {
            ArgumentNullException.ThrowIfNull(condition);

            return new PlantConditionResponse(
                condition.Id,
                condition.Code,
                condition.Name,
                condition.ScientificName,
                condition.ConditionType,
                condition.Description,
                condition.RevisionNumber,
                condition.IsActive,
                condition.CreatedAt,
                condition.RetiredAt,
                condition.Version);
        }
    }
}
