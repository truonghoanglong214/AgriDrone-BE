namespace AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;

internal sealed class BlackboxDecodeConflictException(Exception innerException)
    : Exception("This Blackbox log was decoded concurrently.", innerException);
