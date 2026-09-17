using AgriDrone.Modules.Missions.Domain.Telemetry;

namespace AgriDrone.Modules.Missions.Application
    .Abstractions.Telemetry;

internal interface IMissionTelemetryRepository
{
    Task<MissionTelemetryImport?> GetImportByOperationIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<MissionTelemetryImport?> GetImportByMissionAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        CancellationToken cancellationToken = default);

    void Add(
        MissionTelemetryImport telemetryImport,
        IReadOnlyCollection<MissionTelemetryPoint> points);
}