using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Drones;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.RegisterDrone;

public sealed record RegisterDroneResponse(
    Guid Id,
    Guid TenantId,
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
    DateTimeOffset CreatedAt);