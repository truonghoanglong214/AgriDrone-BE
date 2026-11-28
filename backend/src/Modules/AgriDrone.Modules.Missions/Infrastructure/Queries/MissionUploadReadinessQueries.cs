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
        var uploadRevisionId = await dbContext.DroneMissions.AsNoTracking()
            .Where(mission => mission.Id == missionId && mission.TenantId == tenantId && mission.FarmId == farmId)
            .Select(mission => mission.UploadRevisionId)
            .SingleOrDefaultAsync(cancellationToken);
        var revisionSessions = dbContext.MediaUploadSessions.AsNoTracking()
            .Where(session => session.MissionId == missionId && session.TenantId == tenantId &&
                session.FarmId == farmId && session.UploadRevisionId == uploadRevisionId);

        var mediaCounts = await dbContext.MissionMedia
            .AsNoTracking()
            .Where(link =>
                link.MissionId == missionId &&
                link.Mission.TenantId == tenantId &&
                link.Mission.FarmId == farmId &&
                (uploadRevisionId == Guid.Empty || revisionSessions.Any(session =>
                    session.MediaAssetId == link.MediaId && session.Status == MediaUploadSessionStatus.Completed)) &&
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
                    telemetryImport.MissionId == missionId &&
                    telemetryImport.UploadRevisionId == uploadRevisionId)
                .Select(telemetryImport =>
                    (int?)telemetryImport.PointCount)
                .SingleOrDefaultAsync(cancellationToken);

        var persistedPointCount = uploadRevisionId != Guid.Empty && !importedPointCount.HasValue
            ? 0
            : await dbContext.MissionTelemetryPoints
                .AsNoTracking()
                .CountAsync(
                    point =>
                        point.MissionId == missionId &&
                        point.Mission.TenantId == tenantId &&
                        point.Mission.FarmId == farmId,
                    cancellationToken);

        var hasActiveUploadSessions =
            await revisionSessions
                .AnyAsync(
                    session =>
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
