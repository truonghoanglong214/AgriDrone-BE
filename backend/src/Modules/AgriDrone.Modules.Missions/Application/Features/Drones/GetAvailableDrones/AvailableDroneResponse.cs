using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Drones;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;

public sealed record AvailableDroneResponse(
    Guid Id,
    string Code,
    string Name,
    string? Model,
    string? Manufacturer,
    JsonElement Specifications,
    string? RegistrationNumber,
    DateOnly? RegistrationExpiryDate,
    decimal? WeightKg,
    DroneStatus Status,
    DateTimeOffset? NextMaintenanceAt);