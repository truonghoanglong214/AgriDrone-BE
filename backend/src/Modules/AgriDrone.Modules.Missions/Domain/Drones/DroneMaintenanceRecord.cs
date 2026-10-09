namespace AgriDrone.Modules.Missions.Domain.Drones;

public sealed class DroneMaintenanceRecord
{
    private DroneMaintenanceRecord() { }

    private DroneMaintenanceRecord(Guid droneId, DateTimeOffset startedAt,
        Guid? startedBy, string? reason)
    {
        Id = Guid.NewGuid();
        DroneId = droneId;
        StartedAt = startedAt;
        StartedBy = startedBy;
        Reason = reason;
    }

    public Guid Id { get; private set; }
    public Guid DroneId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public Guid? StartedBy { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public DroneStatus? ClosingStatus { get; private set; }
    public DateTimeOffset? NextMaintenanceAt { get; private set; }

    public static DroneMaintenanceRecord Start(Guid droneId, DateTimeOffset startedAt,
        Guid? startedBy, string? reason)
    {
        if (droneId == Guid.Empty || startedAt == default)
            throw new ArgumentException("Drone and start time are required.");
        if (reason?.Length > 1000)
            throw new ArgumentOutOfRangeException(nameof(reason));
        return new DroneMaintenanceRecord(droneId, startedAt, startedBy,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
    }

    public void Close(DateTimeOffset closedAt, Guid closedBy, DroneStatus closingStatus,
        DateTimeOffset? nextMaintenanceAt)
    {
        if (ClosedAt is not null || closedAt < StartedAt || closedBy == Guid.Empty ||
            closingStatus is not (DroneStatus.Available or DroneStatus.Retired))
            throw new InvalidOperationException("Invalid maintenance closure.");
        ClosedAt = closedAt;
        ClosedBy = closedBy;
        ClosingStatus = closingStatus;
        NextMaintenanceAt = nextMaintenanceAt;
    }
}
