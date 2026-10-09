using System.Text.Json;

namespace AgriDrone.Api.Contracts.Drones;

public sealed record UpdateDroneRequest(
    uint ExpectedVersion,
    string Name,
    string? Model,
    string? Manufacturer,
    JsonElement? Specifications,
    string? SerialNumber,
    string? RegistrationNumber,
    DateOnly? RegistrationDate,
    DateOnly? RegistrationExpiryDate,
    decimal? WeightKg,
    string? Notes);
