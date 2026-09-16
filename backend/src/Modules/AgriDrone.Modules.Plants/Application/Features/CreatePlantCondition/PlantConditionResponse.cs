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
        long Version);
}
