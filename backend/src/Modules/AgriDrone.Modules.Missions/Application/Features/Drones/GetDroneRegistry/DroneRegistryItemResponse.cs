using AgriDrone.Modules.Missions.Domain.Drones;
using System.Text.Json;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;

public sealed record DroneRegistryItemResponse(
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
    DateTimeOffset? LastMaintenanceAt,
    DateTimeOffset? NextMaintenanceAt,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
