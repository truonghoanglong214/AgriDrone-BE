using AgriDrone.Modules.Plants.Domain.Conditions;

namespace AgriDrone.Modules.Plants.Application.Features.GetActivePlantConditions;

public sealed record PlantConditionCatalogResponse(
    Guid Id,
    string Code,
    string Name,
    string? ScientificName,
    ConditionType ConditionType,
    string? Description,
    int RevisionNumber);
