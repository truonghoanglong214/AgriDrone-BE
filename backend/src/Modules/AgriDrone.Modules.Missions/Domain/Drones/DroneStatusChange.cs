using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Drones;

public sealed class DroneStatusChange : Entity
{
    private DroneStatusChange()
    {
    }

    private DroneStatusChange(
        Guid id,
        Guid tenantId,
        Guid droneId,
        DroneStatus? previousStatus,
        DroneStatus newStatus,
        Guid changedBy,
        DateTimeOffset changedAt)
    {
        Id = id;
        TenantId = tenantId;
        DroneId = droneId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedBy = changedBy;
        ChangedAt = changedAt;
    }

    public Guid TenantId { get; private set; }

    public Guid DroneId { get; private set; }

    public DroneStatus? PreviousStatus { get; private set; }

    public DroneStatus NewStatus { get; private set; }

    public Guid ChangedBy { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public static DroneStatusChange Create(
        Guid tenantId,
        Guid droneId,
        DroneStatus? previousStatus,
        DroneStatus newStatus,
        Guid changedBy,
        DateTimeOffset changedAt)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenant ID is required.",
                nameof(tenantId));
        }

        if (droneId == Guid.Empty)
        {
            throw new ArgumentException(
                "Drone ID is required.",
                nameof(droneId));
        }

        if (changedBy == Guid.Empty)
        {
            throw new ArgumentException(
                "Changed-by user ID is required.",
                nameof(changedBy));
        }

        if (changedAt == default)
        {
            throw new ArgumentException(
                "Changed timestamp is required.",
                nameof(changedAt));
        }

        return new DroneStatusChange(
            Guid.NewGuid(),
            tenantId,
            droneId,
            previousStatus,
            newStatus,
            changedBy,
            changedAt);
    }
}