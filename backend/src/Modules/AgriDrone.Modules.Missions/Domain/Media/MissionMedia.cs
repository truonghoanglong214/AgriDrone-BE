using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Domain.Media;

public sealed class MissionMedia
{
    private MissionMedia()
    {
    }

    public Guid MissionId { get; private set; }

    public Guid MediaId { get; private set; }

    public MissionMediaRole MediaRole { get; private set; }

    public DateTimeOffset? CapturedAt { get; private set; }

    public long? TelemetryTimeOffsetMs { get; private set; }

    public string? CaptureClockSource { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DroneMission Mission { get; private set; } = null!;

    public MediaAsset Media { get; private set; } = null!;
}
