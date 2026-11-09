using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMaps;

internal sealed class GetFarmBaseMapsQueryHandler(
    FarmsDbContext dbContext)
    : IRequestHandler<GetFarmBaseMapsQuery, Result<IReadOnlyList<FarmBaseMapSummaryResponse>>>
{
    private sealed record PoleCountRow(Guid VersionId, int Count);
    private sealed record MissionInfoRow(Guid Id, string MissionCode, DateTimeOffset? StartedAt, DateTimeOffset? ScheduledAt);

    public async Task<Result<IReadOnlyList<FarmBaseMapSummaryResponse>>> Handle(
        GetFarmBaseMapsQuery request,
        CancellationToken cancellationToken)
    {
        var farmExists = await dbContext.Farms.AsNoTracking()
            .AnyAsync(f => f.Id == request.FarmId, cancellationToken);

        if (!farmExists)
        {
            return Result.Failure<IReadOnlyList<FarmBaseMapSummaryResponse>>(
                AppError.NotFound("Farm.NotFound", $"Farm with ID {request.FarmId} was not found."));
        }

        var query = dbContext.FarmBaseMapVersions.AsNoTracking()
            .Where(v => v.FarmId == request.FarmId);

        if (!request.IncludeDraft)
        {
            query = query.Where(v => v.Status != FarmBaseMapStatus.Draft);
        }

        var versions = await query
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        if (versions.Count == 0)
        {
            return Result.Success<IReadOnlyList<FarmBaseMapSummaryResponse>>([]);
        }

        // 1. Thống kê số lượng cây đã publish/assign cho từng version
        var publishedPoleCounts = await dbContext.Database.SqlQueryRaw<PoleCountRow>(
            @"SELECT farm_base_map_version_id AS ""VersionId"", COUNT(*)::int AS ""Count""
              FROM plant.plants
              WHERE farm_id = {0} AND farm_base_map_version_id IS NOT NULL
              GROUP BY farm_base_map_version_id",
            request.FarmId)
            .ToListAsync(cancellationToken);

        var countByVersion = publishedPoleCounts.ToDictionary(r => r.VersionId, r => r.Count);

        // 2. Với các bản nháp (Draft), lấy số lượng candidate đã map trong batch
        var draftPoleCounts = await dbContext.Database.SqlQueryRaw<PoleCountRow>(
            @"SELECT b.farm_base_map_version_id AS ""VersionId"", COUNT(c.candidate_id)::int AS ""Count""
              FROM farm.mapping_candidate_batches b
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

            var previewUrl = v.SourceMissionId.HasValue
                ? $"http://localhost:9000/agridrone-media/tenants/{v.TenantId:D}/media/{v.SourceMissionId.Value:D}/map_preview.jpg"
                : null;

            var totalPoles = countByVersion.GetValueOrDefault(v.Id, 0);

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
                PreviewUrl: previewUrl,
                PublishedAt: v.PublishedAt,
                PublishedBy: v.PublishedBy,
                CreatedAt: v.CreatedAt));
        }

        return Result.Success<IReadOnlyList<FarmBaseMapSummaryResponse>>(results);
    }
}

