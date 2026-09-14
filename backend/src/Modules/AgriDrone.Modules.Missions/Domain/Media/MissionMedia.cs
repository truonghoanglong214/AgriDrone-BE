using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Domain;

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

    public static MissionMedia Create(
    Guid missionId,
    Guid mediaId,
    MissionMediaRole mediaRole,
    DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(missionId);
        DomainGuard.NotEmpty(mediaId);
        DomainGuard.Utc(createdAt);

        if (!Enum.IsDefined(mediaRole))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mediaRole));
        }

        return new MissionMedia
        {
            MissionId = missionId,
            MediaId = mediaId,
            MediaRole = mediaRole,
            CreatedAt = createdAt
        };
    }

}
