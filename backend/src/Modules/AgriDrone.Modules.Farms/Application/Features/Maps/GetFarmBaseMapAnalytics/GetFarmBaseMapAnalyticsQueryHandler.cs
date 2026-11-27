using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapAnalytics;

internal sealed class GetFarmBaseMapAnalyticsQueryHandler(
    FarmsDbContext dbContext,
    FarmBaseMapReadAccess readAccess,
    IFarmBaseMapPreviewReader previewReader)
    : IRequestHandler<GetFarmBaseMapAnalyticsQuery, Result<FarmBaseMapAnalyticsResponse>>
{
    private sealed record PoleCountRow(Guid VersionId, int Count);
    private sealed record AreaRow(double AreaHa);

    public async Task<Result<FarmBaseMapAnalyticsResponse>> Handle(
        GetFarmBaseMapAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var farm = await dbContext.Farms.AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == request.FarmId, cancellationToken);
        if (farm is null)
        {
            return Result.Failure<FarmBaseMapAnalyticsResponse>(
                AppError.NotFound("Farm.NotFound", $"Farm with ID {request.FarmId} was not found."));
        }

        var access = await readAccess.CheckAsync(farm.TenantId, farm.Id, cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<FarmBaseMapAnalyticsResponse>(
                AppError.NotFound("Farm.NotFound", "Farm was not found."));
        }

        var versions = await dbContext.FarmBaseMapVersions.AsNoTracking()
            .Where(v => v.FarmId == request.FarmId && v.Status != FarmBaseMapStatus.Draft)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        if (!access.CanReadDraft)
        {
            var readable = new List<FarmBaseMapVersion>(versions.Count);
            foreach (var version in versions)
            {
                if (await previewReader.CanOwnerReadAsync(
                        version.TenantId, version.FarmId, version.SourceSurveyOrderId, cancellationToken))
                {
                    readable.Add(version);
                }
            }

            versions = readable;
        }

        if (versions.Count == 0)
        {
            return Result.Success(new FarmBaseMapAnalyticsResponse(
                FarmId: request.FarmId,
                TotalMappingVersions: 0,
                FirstMappedAt: null,
                LatestMappedAt: null,
                FarmAreaHa: null,
                Timeline: []));
        }

        // 1. Tính diện tích Farm theo ranh giới đã duyệt (hecta)
        double? areaHa = null;
        var areaResult = await dbContext.Database.SqlQueryRaw<AreaRow>(
            @"SELECT ST_Area(geometry::geography) / 10000.0 AS ""AreaHa""
              FROM farm.farm_boundaries
              WHERE farm_id = {0} AND status = 'APPROVED'
              ORDER BY reviewed_at DESC NULLS LAST, created_at DESC
              LIMIT 1",
            request.FarmId)
            .FirstOrDefaultAsync(cancellationToken);

        if (areaResult != null && areaResult.AreaHa > 0)
        {
            areaHa = Math.Round(areaResult.AreaHa, 2);
        }

        // 2. Thống kê số lượng trụ của từng version
        var poleCounts = await dbContext.Database.SqlQueryRaw<PoleCountRow>(
            @"SELECT b.farm_base_map_version_id AS ""VersionId"", COUNT(c.candidate_id)::int AS ""Count""
              FROM farm.mapping_candidate_batches b
              JOIN farm.mapping_candidates c ON c.batch_id = b.id
              WHERE b.farm_id = {0} AND b.farm_base_map_version_id IS NOT NULL
                AND c.review_decision IN ('CreateNew', 'MatchExisting', 'Relocate', 'Replace')
              GROUP BY b.farm_base_map_version_id",
            request.FarmId)
            .ToListAsync(cancellationToken);

        var countByVersion = poleCounts.ToDictionary(r => r.VersionId, r => r.Count);

        // Lấy số lượng cây hoạt động từ plant.plants + observations (bao gồm MatchExisting từ v2+)
        var plantCounts = await FarmBaseMapPlantQueries.GetActivePoleCountsByVersionAsync(
            dbContext, request.FarmId, cancellationToken);

        foreach (var (versionId, count) in plantCounts)
        {
            countByVersion[versionId] = count;
        }

        // Lấy số lượng biến động thủ công từ bảng phụ plant_change_events
        var manualChangesByVersion = await FarmBaseMapPlantQueries.GetManualChangeCountsByVersionAsync(
            dbContext, request.FarmId, cancellationToken);

        // 3. Xây dựng timeline biến động
        var timeline = new List<BaseMapVersionMetricDto>();
        var previousPoles = 0;

        for (var i = 0; i < versions.Count; i++)
        {
            var v = versions[i];
            var poles = countByVersion.GetValueOrDefault(v.Id, 0);
            var delta = i == 0 ? 0 : poles - previousPoles;
            previousPoles = poles;

            double? density = null;
            if (areaHa.HasValue && areaHa.Value > 0 && poles > 0)
            {
                density = Math.Round(poles / areaHa.Value, 1);
            }

            var manualChanges = manualChangesByVersion.GetValueOrDefault(v.Id, 0);

            timeline.Add(new BaseMapVersionMetricDto(
                VersionId: v.Id,
                VersionNumber: v.VersionNumber,
                Status: v.Status.ToString().ToUpperInvariant(),
                PublishedAt: v.PublishedAt,
                TotalPoles: poles,
                PolesDelta: delta,
                DensityPolesPerHa: density,
                PlantSpacingM: v.PlantSpacingM,
                RowSpacingM: v.RowSpacingM,
                GridBearingDeg: v.GridBearingDeg,
                ManualChangesCount: manualChanges));
        }

        var firstVersion = versions[0];
        var latestVersion = versions[^1];

        var response = new FarmBaseMapAnalyticsResponse(
            FarmId: request.FarmId,
            TotalMappingVersions: versions.Count,
            FirstMappedAt: firstVersion.PublishedAt ?? firstVersion.CreatedAt,
            LatestMappedAt: latestVersion.PublishedAt ?? latestVersion.CreatedAt,
            FarmAreaHa: areaHa,
            Timeline: timeline);

        return Result.Success(response);
    }
}

