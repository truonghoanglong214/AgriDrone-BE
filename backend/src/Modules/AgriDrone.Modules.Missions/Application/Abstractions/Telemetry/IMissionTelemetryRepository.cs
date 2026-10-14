using AgriDrone.Modules.Missions.Domain.Telemetry;

namespace AgriDrone.Modules.Missions.Application
    .Abstractions.Telemetry;

internal interface IMissionTelemetryRepository
{
    Task<BlackboxDecode?> GetDecodeAsync(
        Guid tenantId, Guid farmId, Guid missionId,
        string sourceChecksum, CancellationToken cancellationToken = default);

    void AddDecode(BlackboxDecode decode);

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
