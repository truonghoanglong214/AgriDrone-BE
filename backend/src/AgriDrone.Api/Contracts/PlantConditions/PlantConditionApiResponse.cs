namespace AgriDrone.Api.Contracts.PlantConditions;

public sealed record PlantConditionApiResponse(
    Guid Id,
    string Code,
    string Name,
    string? ScientificName,
    PlantConditionTypeValue ConditionType,
    string? Description,
    int RevisionNumber,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RetiredAt,
    long Version);
