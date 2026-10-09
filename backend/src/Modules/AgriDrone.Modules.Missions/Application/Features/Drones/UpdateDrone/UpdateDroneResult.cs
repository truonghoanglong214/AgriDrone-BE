using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Drones;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.UpdateDrone;

public sealed record UpdateDroneResult(
    Guid Id,
    string Code,
    string Name,
    string? Model,
    string? Manufacturer,
    JsonElement Specifications,
    string? SerialNumber,
    string? RegistrationNumber,
    DateOnly? RegistrationDate,
    DateOnly? RegistrationExpiryDate,
    decimal? WeightKg,
    DroneStatus Status,
    string? Notes,
    DateTimeOffset UpdatedAt,
    uint Version);
