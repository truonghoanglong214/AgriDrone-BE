using System.Text.Json;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.UpdateDrone;

public sealed record UpdateDroneCommand(
    Guid DroneId,
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
    string? Notes)
    : IRequest<Result<UpdateDroneResult>>;
