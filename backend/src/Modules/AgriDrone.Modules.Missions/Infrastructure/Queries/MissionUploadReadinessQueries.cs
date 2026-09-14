using AgriDrone.Modules.Missions.Application
    .Abstractions.Media;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class MissionUploadReadinessQueries(
    MissionsDbContext dbContext)
    : IMissionUploadReadinessQueries
{
    public async Task<MissionUploadReadinessSnapshot> GetAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        var mediaCounts = await dbContext.MissionMedia
            .AsNoTracking()
            .Where(link =>
                link.MissionId == missionId &&
                link.Mission.TenantId == tenantId &&
                link.Mission.FarmId == farmId &&
                link.Media.StorageStatus ==
                    MediaStorageStatus.Active)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                RawImageCount = group.Count(link =>
                    link.MediaRole ==
                    MissionMediaRole.RawImage),

                RawVideoCount = group.Count(link =>
                    link.MediaRole ==
                    MissionMediaRole.RawVideo)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var importedPointCount =
            await dbContext.MissionTelemetryImports
                .AsNoTracking()
                .Where(telemetryImport =>
                    telemetryImport.TenantId == tenantId &&
                    telemetryImport.FarmId == farmId &&
                    telemetryImport.MissionId == missionId)
                .Select(telemetryImport =>
                    (int?)telemetryImport.PointCount)
                .SingleOrDefaultAsync(cancellationToken);

        var persistedPointCount =
            await dbContext.MissionTelemetryPoints
                .AsNoTracking()
                .CountAsync(
                    point =>
                        point.MissionId == missionId &&
                        point.Mission.TenantId == tenantId &&
                        point.Mission.FarmId == farmId,
                    cancellationToken);

        var hasActiveUploadSessions =
            await dbContext.MediaUploadSessions
                .AsNoTracking()
                .AnyAsync(
                    session =>
                        session.TenantId == tenantId &&
                        session.FarmId == farmId &&
                        session.MissionId == missionId &&
                        (
                            session.Status ==
                                MediaUploadSessionStatus.Verifying ||
                            (
                                session.Status ==
                                    MediaUploadSessionStatus.Pending &&
                                session.ExpiresAt > evaluatedAt
                            )
                        ),
                    cancellationToken);

        return new MissionUploadReadinessSnapshot(
            mediaCounts?.RawImageCount ?? 0,
            mediaCounts?.RawVideoCount ?? 0,
            importedPointCount.HasValue,
            importedPointCount ?? 0,
            persistedPointCount,
            hasActiveUploadSessions);
    }
}