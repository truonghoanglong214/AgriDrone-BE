using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Drones;

public sealed class Drone : AggregateRoot
{
    private Drone()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Model { get; private set; }

    public string? SerialNumber { get; private set; }

    public DroneStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }
}
