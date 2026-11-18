using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMaps;

internal sealed class GetFarmBaseMapsQueryHandler(
    FarmsDbContext dbContext,
    FarmBaseMapReadAccess readAccess,
    IFarmBaseMapPreviewReader previewReader)
    : IRequestHandler<GetFarmBaseMapsQuery, Result<IReadOnlyList<FarmBaseMapSummaryResponse>>>
{
    private sealed record PoleCountRow(Guid VersionId, int Count);
    private sealed record MissionInfoRow(Guid Id, string MissionCode, DateTimeOffset? StartedAt, DateTimeOffset? ScheduledAt);

    public async Task<Result<IReadOnlyList<FarmBaseMapSummaryResponse>>> Handle(
        GetFarmBaseMapsQuery request,
        CancellationToken cancellationToken)
    {
        var farm = await dbContext.Farms.AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == request.FarmId, cancellationToken);

        if (farm is null)
        {
            return Result.Failure<IReadOnlyList<FarmBaseMapSummaryResponse>>(
                AppError.NotFound("Farm.NotFound", $"Farm with ID {request.FarmId} was not found."));
        }

        var access = await readAccess.CheckAsync(farm.TenantId, farm.Id, cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<IReadOnlyList<FarmBaseMapSummaryResponse>>(
                AppError.NotFound("Farm.NotFound", "Farm was not found."));
        }

        var query = dbContext.FarmBaseMapVersions.AsNoTracking()
            .Where(v => v.FarmId == request.FarmId);

        if (!request.IncludeDraft || !access.CanReadDraft)
        {
            query = query.Where(v => v.Status != FarmBaseMapStatus.Draft);
        }

        var versions = await query
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        if (!access.CanReadDraft)
        {
            var readable = new List<FarmBaseMapVersion>(versions.Count);
            foreach (var version in versions)
            {
                if (await previewReader.CanOwnerReadAsync(
                        version.TenantId, version.FarmId, version.SourceSurveyOrderId, cancellationToken))
                    readable.Add(version);
            }
            versions = readable;
        }

        if (versions.Count == 0)
        {
            return Result.Success<IReadOnlyList<FarmBaseMapSummaryResponse>>([]);
        }

        // 1. Thống kê số lượng cây đang hoạt động (không tính cây đã xóa) cho từng version
        var publishedPoleCounts = await dbContext.Database.SqlQueryRaw<PoleCountRow>(
            @"SELECT farm_base_map_version_id AS ""VersionId"", COUNT(*)::int AS ""Count""
              FROM plant.plants
              WHERE farm_id = {0} AND farm_base_map_version_id IS NOT NULL
                AND lifecycle_status != 'REMOVED'
              GROUP BY farm_base_map_version_id",
            request.FarmId)
            .ToListAsync(cancellationToken);

        var countByVersion = publishedPoleCounts.ToDictionary(r => r.VersionId, r => r.Count);

        // 2. Thống kê số lượng biến động thủ công từ bảng phụ plant_change_events cho từng version
        var manualChangeCounts = await dbContext.Database.SqlQueryRaw<PoleCountRow>(
            @"SELECT farm_base_map_version_id AS ""VersionId"", COUNT(*)::int AS ""Count""
              FROM plant.plant_change_events
              WHERE farm_id = {0} AND farm_base_map_version_id IS NOT NULL
              GROUP BY farm_base_map_version_id",
            request.FarmId)
            .ToListAsync(cancellationToken);

        var manualChangesByVersion = manualChangeCounts.ToDictionary(r => r.VersionId, r => r.Count);

        // 3. Với các bản nháp (Draft), lấy số lượng candidate đã map trong batch
        var draftPoleCounts = await dbContext.Database.SqlQueryRaw<PoleCountRow>(
            @"SELECT b.farm_base_map_version_id AS ""VersionId"", COUNT(c.candidate_id)::int AS ""Count""
              FROM farm.mapping_candidate_batches b
              JOIN farm.mapping_candidate_batches b2 ON b2.id = b.id
              JOIN farm.mapping_candidates c ON c.batch_id = b.id
              WHERE b.farm_id = {0} AND b.farm_base_map_version_id IS NOT NULL
                AND c.review_decision != 'Reject'
              GROUP BY b.farm_base_map_version_id",
            request.FarmId)
            .ToListAsync(cancellationToken);

        foreach (var row in draftPoleCounts)
        {
            if (!countByVersion.TryGetValue(row.VersionId, out var existingCount) || existingCount == 0)
            {
                countByVersion[row.VersionId] = row.Count;
            }
        }

        // 3. Lấy thông tin Mission nguồn nếu có
        var missionIds = versions
            .Where(v => v.SourceMissionId.HasValue)
            .Select(v => v.SourceMissionId!.Value)
            .Distinct()
            .ToList();

        var missionInfoMap = new Dictionary<Guid, MissionInfoRow>();
        if (missionIds.Count > 0)
        {
            var missions = await dbContext.Database.SqlQueryRaw<MissionInfoRow>(
                @"SELECT id AS ""Id"", mission_code AS ""MissionCode"", started_at AS ""StartedAt"", scheduled_at AS ""ScheduledAt""
                  FROM mission.drone_missions
                  WHERE farm_id = {0}",
                request.FarmId)
                .ToListAsync(cancellationToken);

            missionInfoMap = missions.ToDictionary(m => m.Id, m => m);
        }

        var results = new List<FarmBaseMapSummaryResponse>(versions.Count);
        foreach (var v in versions)
        {
            MissionInfoRow? missionInfo = null;
            if (v.SourceMissionId.HasValue)
            {
                missionInfoMap.TryGetValue(v.SourceMissionId.Value, out missionInfo);
            }

            var preview = v.SourceMissionId.HasValue
                ? await previewReader.GetAsync(v.TenantId, v.FarmId, v.SourceMissionId.Value, cancellationToken)
                : null;

            var totalPoles = countByVersion.GetValueOrDefault(v.Id, 0);
            var manualChangesCount = manualChangesByVersion.GetValueOrDefault(v.Id, 0);

            results.Add(new FarmBaseMapSummaryResponse(
                VersionId: v.Id,
                FarmId: v.FarmId,
                VersionNumber: v.VersionNumber,
                Status: v.Status.ToString().ToUpperInvariant(),
                SourceMissionId: v.SourceMissionId,
                SourceMissionCode: missionInfo?.MissionCode,
                FlightDate: missionInfo?.StartedAt ?? missionInfo?.ScheduledAt ?? v.CreatedAt,
                SourceSurveyOrderId: v.SourceSurveyOrderId,
                FarmBoundaryVersionId: v.FarmBoundaryVersionId,
                GridBearingDeg: v.GridBearingDeg,
                RowSpacingM: v.RowSpacingM,
                PlantSpacingM: v.PlantSpacingM,
                TotalPoles: totalPoles,
                PreviewUrl: preview?.Url,
                PublishedAt: v.PublishedAt,
                PublishedBy: v.PublishedBy,
                CreatedAt: v.CreatedAt,
                ManualChangesCount: manualChangesCount));
        }

        return Result.Success<IReadOnlyList<FarmBaseMapSummaryResponse>>(results);
    }
}

