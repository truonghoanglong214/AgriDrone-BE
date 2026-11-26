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

    Task<bool> SaveCaptureImportAsync(
        Guid orderId,
        Guid attemptId,
        MissionTelemetryImport telemetryImport,
        IReadOnlyCollection<MissionTelemetryPoint> points,
        bool replaceExisting,
        CancellationToken cancellationToken);

    Task ReplaceAsync(
        MissionTelemetryImport telemetryImport,
        IReadOnlyCollection<MissionTelemetryPoint> points,
        CancellationToken cancellationToken = default);
}
