using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Missions.Domain.Telemetry;

public sealed class MissionTelemetryPoint : Entity
{
    private const decimal MaxAltitudeMagnitudeM =
        999_999.999m;

    private const decimal MaxSpeedOrAccuracyM =
        99_999.999m;

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

    public static MissionTelemetryPoint Create(
        Guid missionId,
        int sequenceNumber,
        DateTimeOffset recordedAt,
        double longitude,
        double latitude,
        decimal? altitudeM,
        AltitudeReference? altitudeReference,
        decimal? headingDeg,
        decimal? speedMps,
        decimal? horizontalAccuracyM,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(missionId);
        DomainGuard.Utc(recordedAt);
        DomainGuard.Utc(createdAt);

        if (sequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                "Sequence number cannot be negative.");
        }

        if (!double.IsFinite(longitude) ||
            longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                "Longitude must be finite and in the range [-180, 180].");
        }

        if (!double.IsFinite(latitude) ||
            latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                "Latitude must be finite and in the range [-90, 90].");
        }

        if (altitudeM is < -MaxAltitudeMagnitudeM or
            > MaxAltitudeMagnitudeM)
        {
            throw new ArgumentOutOfRangeException(
                nameof(altitudeM),
                "Altitude is outside the supported database range.");
        }

        if (altitudeReference.HasValue &&
            !Enum.IsDefined(altitudeReference.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(altitudeReference));
        }

        if (headingDeg is < 0 or >= 360)
        {
            throw new ArgumentOutOfRangeException(
                nameof(headingDeg),
                "Heading must be in the range [0, 360).");
        }

        if (speedMps is < 0 or > MaxSpeedOrAccuracyM)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speedMps),
                "Speed is outside the supported range.");
        }

        if (horizontalAccuracyM is < 0 or
            > MaxSpeedOrAccuracyM)
        {
            throw new ArgumentOutOfRangeException(
                nameof(horizontalAccuracyM),
                "Horizontal accuracy is outside the supported range.");
        }

        var location = new Point(
            longitude,
            latitude)
        {
            SRID = 4326
        };

        return new MissionTelemetryPoint
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            SequenceNumber = sequenceNumber,
            RecordedAt = recordedAt,
            Location = location,
            AltitudeM = altitudeM,
            AltitudeReference = altitudeReference,
            HeadingDeg = headingDeg,
            SpeedMps = speedMps,
            HorizontalAccuracyM = horizontalAccuracyM,
            CreatedAt = createdAt
        };
    }
}