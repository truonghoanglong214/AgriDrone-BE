namespace AgriDrone.Modules.Missions.Application
    .Abstractions.Telemetry;

internal sealed class TelemetryImportConflictException(
    Exception innerException)
    : Exception(
        "Telemetry data was inserted concurrently " +
        "or conflicts with existing mission telemetry.",
        innerException);