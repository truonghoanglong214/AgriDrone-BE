namespace AgriDrone.Api.Contracts.PlantConditions;

public sealed record PlantConditionCatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? ScientificName,
    PlantConditionTypeValue ConditionType,
    string? Description,
    int RevisionNumber,
    PlantHealthLabelKindValue? PlantHealthLabelKind,
    string? DatasetVersion,
    string? AnnotationRuleVersion,
    string? TeamLabelLimitation);
