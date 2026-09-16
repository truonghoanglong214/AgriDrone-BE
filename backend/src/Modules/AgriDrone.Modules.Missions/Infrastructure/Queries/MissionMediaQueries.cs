using System.Linq.Expressions;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Features.Media;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class MissionMediaQueries(MissionsDbContext dbContext) : IMissionMediaQueries
{
    private static readonly Expression<Func<MissionMedia, MissionMediaResponse>> Projection =
        item => new MissionMediaResponse(
            item.MediaId, item.MissionId, item.Media.MediaType, item.MediaRole,
            item.Media.MimeType, item.Media.FileSizeBytes, item.Media.Checksum,
            item.Media.WidthPx, item.Media.HeightPx, item.Media.DurationMs,
            item.CapturedAt, item.TelemetryTimeOffsetMs, item.CaptureClockSource, item.CreatedAt);

    public Task<bool> MissionExistsAsync(
        Guid tenantId, Guid farmId, Guid missionId, CancellationToken cancellationToken) =>
        dbContext.DroneMissions.AsNoTracking().AnyAsync(
            mission => mission.Id == missionId &&
                mission.TenantId == tenantId && mission.FarmId == farmId,
            cancellationToken);

    public Task<PagedResult<MissionMediaResponse>> GetPageAsync(
        Guid tenantId, Guid farmId, Guid missionId, PagedRequest page,
        MediaType? mediaType, MissionMediaRole? mediaRole, CancellationToken cancellationToken)
    {
        var query = ScopedMedia(tenantId, farmId, missionId);
        if (mediaType.HasValue)
            query = query.Where(item => item.Media.MediaType == mediaType.Value);
        if (mediaRole.HasValue)
            query = query.Where(item => item.MediaRole == mediaRole.Value);

        return query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.MediaId)
            .Select(Projection).ToPagedResultAsync(page, cancellationToken);
    }

    public Task<MissionMediaResponse?> GetDetailsAsync(
        Guid tenantId, Guid farmId, Guid missionId, Guid mediaId, CancellationToken cancellationToken) =>
        ScopedMedia(tenantId, farmId, missionId).Where(item => item.MediaId == mediaId)
            .Select(Projection).SingleOrDefaultAsync(cancellationToken);

    public Task<string?> GetDownloadSourceAsync(
        Guid tenantId, Guid farmId, Guid missionId, Guid mediaId, CancellationToken cancellationToken) =>
        ScopedMedia(tenantId, farmId, missionId).Where(item => item.MediaId == mediaId)
            .Select(item => (string?)item.Media.Url).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<MissionMedia> ScopedMedia(Guid tenantId, Guid farmId, Guid missionId) =>
        VisibleMedia(dbContext.MissionMedia.AsNoTracking(), tenantId, farmId, missionId);

    internal static IQueryable<MissionMedia> VisibleMedia(
        IQueryable<MissionMedia> source, Guid tenantId, Guid farmId, Guid missionId) =>
        source.Where(item =>
            item.MissionId == missionId &&
            item.Mission.TenantId == tenantId && item.Mission.FarmId == farmId &&
            item.Media.TenantId == tenantId && item.Media.FarmId == farmId &&
            item.Media.StorageStatus == MediaStorageStatus.Active &&
            item.Media.DeletedAt == null && item.Media.DeletionRequestedAt == null);
}
