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

    public async Task<bool> SaveCaptureImportAsync(
        Guid orderId,
        Guid attemptId,
        MissionTelemetryImport telemetryImport,
        IReadOnlyCollection<MissionTelemetryPoint> points,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var uploadOpen = await dbContext.Database.SqlQuery<int>($"""
                SELECT 1 AS "Value"
                FROM survey.survey_orders o
                JOIN survey.capture_attempts a ON a.order_id = o.id
                JOIN mission.drone_missions m ON m.capture_attempt_id = a.id AND m.survey_order_id = o.id
                WHERE o.id = {orderId} AND o.tenant_id = {telemetryImport.TenantId} AND o.farm_id = {telemetryImport.FarmId}
                  AND o.scope = 'V3' AND o.status = 'AWAITING_CAPTURE_UPLOAD'
                  AND a.id = {attemptId} AND m.id = {telemetryImport.MissionId}
                  AND m.tenant_id = o.tenant_id AND m.farm_id = o.farm_id
                  AND a.tenant_id = o.tenant_id AND a.farm_id = o.farm_id AND a.status = 'AWAITING_UPLOAD'
                FOR SHARE OF o
                """)
            .ToListAsync(cancellationToken);
        if (uploadOpen.Count == 0)
            return false;

        if (replaceExisting)
        {
            await dbContext.MissionTelemetryPoints
                .Where(point => point.MissionId == telemetryImport.MissionId)
                .ExecuteDeleteAsync(cancellationToken);
            dbContext.MissionTelemetryPoints.AddRange(points);
        }
        else
        {
            Add(telemetryImport, points);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
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
