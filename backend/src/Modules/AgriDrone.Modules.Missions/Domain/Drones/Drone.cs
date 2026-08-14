using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Drones;

public sealed class Drone : AggregateRoot
{
    private Drone()
    {
    }

    public Guid TenantId { get; private set; }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Model { get; private set; }

    public string? Manufacturer { get; private set; }

    public JsonDocument Specifications { get; private set; } = null!;

    public string? SerialNumber { get; private set; }

    public string? RegistrationNumber { get; private set; }

    public DateOnly? RegistrationDate { get; private set; }

    public DateOnly? RegistrationExpiryDate { get; private set; }

    public decimal? WeightKg { get; private set; }

    public DroneStatus Status { get; private set; }

    public DateTimeOffset? LastMaintenanceAt { get; private set; }

    public DateTimeOffset? NextMaintenanceAt { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }
}
