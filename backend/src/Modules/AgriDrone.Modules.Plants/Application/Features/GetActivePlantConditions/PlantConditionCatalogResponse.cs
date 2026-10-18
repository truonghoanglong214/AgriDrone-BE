using AgriDrone.Modules.Plants.Domain.Conditions;

namespace AgriDrone.Modules.Plants.Application.Features.GetActivePlantConditions;

public sealed record PlantConditionCatalogResponse(
    Guid Id,
    string Code,
    string Name,
    string? ScientificName,
    ConditionType ConditionType,
    string? Description,
    int RevisionNumber,
    PlantHealthLabelKind? PlantHealthLabelKind = null,
    string? DatasetVersion = null,
    string? AnnotationRuleVersion = null,
    string? TeamLabelLimitation = null);
