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

    public async Task ReplaceAsync(
        MissionTelemetryImport telemetryImport,
        IReadOnlyCollection<MissionTelemetryPoint> points,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(telemetryImport);
        ArgumentNullException.ThrowIfNull(points);

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        await dbContext.MissionTelemetryPoints
            .Where(point => point.MissionId == telemetryImport.MissionId)
            .ExecuteDeleteAsync(cancellationToken);

        dbContext.MissionTelemetryPoints.AddRange(points);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
