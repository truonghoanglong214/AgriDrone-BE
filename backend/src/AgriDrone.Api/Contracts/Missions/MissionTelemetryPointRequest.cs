using System.Text.Json.Serialization;
using AgriDrone.Modules.Missions.Domain.Telemetry;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record MissionTelemetryPointRequest(
    int SequenceNumber,
    DateTimeOffset RecordedAt,
    double Longitude,
    double Latitude,
    decimal? AltitudeM,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] AltitudeReference? AltitudeReference,
    decimal? HeadingDeg,
    decimal? SpeedMps,
    decimal? HorizontalAccuracyM);