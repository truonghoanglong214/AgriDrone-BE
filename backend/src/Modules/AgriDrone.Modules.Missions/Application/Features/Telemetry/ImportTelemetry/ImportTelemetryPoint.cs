using AgriDrone.Modules.Missions.Domain.Telemetry;

namespace AgriDrone.Modules.Missions.Application.Features.Telemetry.ImportTelemetry;

public sealed record ImportTelemetryPoint(
    int SequenceNumber,
    DateTimeOffset RecordedAt,
    double Longitude,
    double Latitude,
    decimal? AltitudeM,
    AltitudeReference? AltitudeReference,
    decimal? HeadingDeg,
    decimal? SpeedMps,
    decimal? HorizontalAccuracyM);