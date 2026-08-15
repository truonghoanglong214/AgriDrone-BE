using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Missions.Domain.Telemetry;

public sealed class MissionTelemetryPoint : Entity
{
    private MissionTelemetryPoint()
    {
    }

    public Guid MissionId { get; private set; }

    public int SequenceNumber { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public Point Location { get; private set; } = null!;

    public decimal? AltitudeM { get; private set; }

    public AltitudeReference? AltitudeReference { get; private set; }

    public decimal? HeadingDeg { get; private set; }

    public decimal? SpeedMps { get; private set; }

    public decimal? HorizontalAccuracyM { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DroneMission Mission { get; private set; } = null!;
}
