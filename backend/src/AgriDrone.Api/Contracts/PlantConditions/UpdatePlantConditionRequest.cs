namespace AgriDrone.Api.Contracts.PlantConditions;

public sealed record UpdatePlantConditionRequest(
    string Name,
    string? ScientificName,
    string? Description,
    long ExpectedVersion);
