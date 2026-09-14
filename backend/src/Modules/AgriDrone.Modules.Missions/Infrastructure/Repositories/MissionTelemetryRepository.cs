using AgriDrone.Modules.Missions.Application
    .Abstractions.Telemetry;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Repositories;

internal sealed class MissionTelemetryRepository(
    MissionsDbContext dbContext)
    : IMissionTelemetryRepository
{
    public Task<MissionTelemetryImport?>
        GetImportByOperationIdAsync(
            Guid tenantId,
            Guid farmId,
            Guid missionId,
            Guid operationId,
            CancellationToken cancellationToken = default)
    {
        return dbContext.MissionTelemetryImports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                telemetryImport =>
                    telemetryImport.TenantId == tenantId &&
                    telemetryImport.FarmId == farmId &&
                    telemetryImport.MissionId == missionId &&
                    telemetryImport.OperationId == operationId,
                cancellationToken);
    }

    public Task<MissionTelemetryImport?>
        GetImportByMissionAsync(
            Guid tenantId,
            Guid farmId,
            Guid missionId,
            CancellationToken cancellationToken = default)
    {
        return dbContext.MissionTelemetryImports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                telemetryImport =>
                    telemetryImport.TenantId == tenantId &&
                    telemetryImport.FarmId == farmId &&
                    telemetryImport.MissionId == missionId,
                cancellationToken);
    }

    public void Add(
        MissionTelemetryImport telemetryImport,
        IReadOnlyCollection<MissionTelemetryPoint> points)
    {
        ArgumentNullException.ThrowIfNull(telemetryImport);
        ArgumentNullException.ThrowIfNull(points);

        dbContext.MissionTelemetryImports.Add(
            telemetryImport);

        dbContext.MissionTelemetryPoints.AddRange(
            points);
    }
}