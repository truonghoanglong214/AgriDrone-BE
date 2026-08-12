using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Missions.Domain.Missions;

public sealed class DroneMission : AggregateRoot
{
    private DroneMission()
    {
    }

    public Guid FarmId { get; private set; }

    public Guid? ZoneId { get; private set; }

    public Guid DroneId { get; private set; }

    public Guid? PilotUserId { get; private set; }

    public string MissionCode { get; private set; } = null!;

    public MissionType MissionType { get; private set; }

    public MissionStatus Status { get; private set; }

    public ProcessingStatus ProcessingStatus { get; private set; }

    public DateTimeOffset? ScheduledAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public LineString? FlightRoute { get; private set; }

    public int? DetectedPlantCount { get; private set; }

    public string? Notes { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
