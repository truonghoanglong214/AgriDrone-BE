namespace AgriDrone.Api.Contracts.PlantConditions;

public sealed record VersionPlantConditionRequest(
    string Name,
    string? ScientificName,
    string? Description,
    long ExpectedVersion);
