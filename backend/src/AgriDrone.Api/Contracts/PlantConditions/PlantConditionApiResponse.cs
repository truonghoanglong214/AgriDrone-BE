namespace AgriDrone.Api.Contracts.PlantConditions;

public sealed record PlantConditionApiResponse(
    Guid Id,
    string Code,
    string Name,
    string? ScientificName,
    PlantConditionTypeValue ConditionType,
    string? Description,
    PlantHealthLabelKindValue? PlantHealthLabelKind,
    string? DatasetVersion,
    string? AnnotationRuleVersion,
    string? TeamLabelLimitation,
    int RevisionNumber,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RetiredAt,
    long Version);
