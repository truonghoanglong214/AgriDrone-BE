using AgriDrone.Modules.Missions.Domain.Telemetry;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record MissionTelemetryPointRequest(
    int SequenceNumber,
    DateTimeOffset RecordedAt,
    double Longitude,
    double Latitude,
    decimal? AltitudeM,
    AltitudeReference? AltitudeReference,
    decimal? HeadingDeg,
    decimal? SpeedMps,
    decimal? HorizontalAccuracyM);