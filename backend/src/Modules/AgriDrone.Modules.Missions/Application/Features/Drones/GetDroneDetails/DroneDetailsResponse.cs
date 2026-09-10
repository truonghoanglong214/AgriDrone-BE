using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Drones;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneDetails;

public sealed record DroneDetailsResponse(
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
    DateTimeOffset? LastMaintenanceAt,
    DateTimeOffset? NextMaintenanceAt,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);