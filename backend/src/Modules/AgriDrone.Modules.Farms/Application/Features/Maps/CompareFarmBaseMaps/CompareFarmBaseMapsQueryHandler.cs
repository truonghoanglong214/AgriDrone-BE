using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.CompareFarmBaseMaps;

internal sealed class CompareFarmBaseMapsQueryHandler(
    FarmsDbContext dbContext)
    : IRequestHandler<CompareFarmBaseMapsQuery, Result<CompareFarmBaseMapsResponse>>
{
    private sealed record CandidateRow(
        Guid CandidateId,
        Guid? TargetPlantId,
        int? RowIndex,
        int? ColumnIndex,
        double Lat,
        double Lon,
        string ReviewDecision,
        string? TreeId);

    private sealed record ChangeEventRow(
        Guid Id,
        Guid? PlantId,
        string ChangeType,
        double? OldLat,
        double? OldLon,
        double? NewLat,
        double? NewLon,
        int? OldRow,
        int? NewRow,
        int? OldCol,
        int? NewCol,
        string? Notes,
        DateTimeOffset CreatedAt);

    public async Task<Result<CompareFarmBaseMapsResponse>> Handle(
        CompareFarmBaseMapsQuery request,
        CancellationToken cancellationToken)
    {
        var allVersions = await dbContext.FarmBaseMapVersions.AsNoTracking()
            .Where(v => v.FarmId == request.FarmId && v.Status != FarmBaseMapStatus.Draft)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        if (allVersions.Count < 2 && (request.FromVersionId == null || request.ToVersionId == null))
        {
            return Result.Failure<CompareFarmBaseMapsResponse>(
                AppError.Validation("CompareBaseMaps.InsufficientVersions", "At least 2 published base map versions are required to perform a comparison."));
        }

        FarmBaseMapVersion? fromVersion;
        FarmBaseMapVersion? toVersion;

        if (request.FromVersionId.HasValue && request.ToVersionId.HasValue)
        {
            fromVersion = allVersions.FirstOrDefault(v => v.Id == request.FromVersionId.Value)
                ?? await dbContext.FarmBaseMapVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == request.FromVersionId.Value && v.FarmId == request.FarmId, cancellationToken);
            toVersion = allVersions.FirstOrDefault(v => v.Id == request.ToVersionId.Value)
                ?? await dbContext.FarmBaseMapVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == request.ToVersionId.Value && v.FarmId == request.FarmId, cancellationToken);
        }
        else if (request.FromVersionNumber.HasValue && request.ToVersionNumber.HasValue)
        {
            fromVersion = allVersions.FirstOrDefault(v => v.VersionNumber == request.FromVersionNumber.Value);
            toVersion = allVersions.FirstOrDefault(v => v.VersionNumber == request.ToVersionNumber.Value);
        }
        else
        {
            // Default: 2 phiên bản mới nhất
            toVersion = allVersions[^1];
            fromVersion = allVersions[^2];
        }

        if (fromVersion is null)
        {
            return Result.Failure<CompareFarmBaseMapsResponse>(
                AppError.NotFound("CompareBaseMaps.FromVersionNotFound", "From base map version not found."));
        }

        if (toVersion is null)
        {
            return Result.Failure<CompareFarmBaseMapsResponse>(
                AppError.NotFound("CompareBaseMaps.ToVersionNotFound", "To base map version not found."));
        }

        // Hoán đổi nếu truyền ngược thứ tự
        if (fromVersion.VersionNumber > toVersion.VersionNumber)
        {
            (fromVersion, toVersion) = (toVersion, fromVersion);
        }

        // 1. Lấy danh sách candidate của toVersion
        var toCandidates = await dbContext.Database.SqlQueryRaw<CandidateRow>(
            @"SELECT c.candidate_id AS ""CandidateId"",
                     COALESCE(c.corrected_matched_plant_id, c.proposed_matched_plant_id) AS ""TargetPlantId"",
                     COALESCE(c.corrected_row_index, c.proposed_row_index) AS ""RowIndex"",
                     COALESCE(c.corrected_column_index, c.proposed_column_index) AS ""ColumnIndex"",
                     ST_Y(COALESCE(c.corrected_position, c.original_position)::geometry) AS ""Lat"",
                     ST_X(COALESCE(c.corrected_position, c.original_position)::geometry) AS ""Lon"",
                     c.review_decision::text AS ""ReviewDecision"",
                     c.tree_id AS ""TreeId""
              FROM farm.mapping_candidates c
              JOIN farm.mapping_candidate_batches b ON b.id = c.batch_id
              WHERE b.farm_base_map_version_id = {0}
                AND c.review_decision IN ('CreateNew', 'MatchExisting', 'Relocate', 'Replace')",
            toVersion.Id)
            .ToListAsync(cancellationToken);

        // 2. Lấy danh sách candidate của fromVersion
        var fromCandidates = await dbContext.Database.SqlQueryRaw<CandidateRow>(
            @"SELECT c.candidate_id AS ""CandidateId"",
                     COALESCE(c.corrected_matched_plant_id, c.proposed_matched_plant_id) AS ""TargetPlantId"",
                     COALESCE(c.corrected_row_index, c.proposed_row_index) AS ""RowIndex"",
                     COALESCE(c.corrected_column_index, c.proposed_column_index) AS ""ColumnIndex"",
                     ST_Y(COALESCE(c.corrected_position, c.original_position)::geometry) AS ""Lat"",
                     ST_X(COALESCE(c.corrected_position, c.original_position)::geometry) AS ""Lon"",
                     c.review_decision::text AS ""ReviewDecision"",
                     c.tree_id AS ""TreeId""
              FROM farm.mapping_candidates c
              JOIN farm.mapping_candidate_batches b ON b.id = c.batch_id
              WHERE b.farm_base_map_version_id = {0}
                AND c.review_decision IN ('CreateNew', 'MatchExisting', 'Relocate', 'Replace')",
            fromVersion.Id)
            .ToListAsync(cancellationToken);

        var polesBefore = fromCandidates.Count;
        var polesAfter = toCandidates.Count;

        // 3. Truy vấn các sự kiện biến động cây đã ghi lại giữa 2 đợt
        var fromDate = fromVersion.PublishedAt ?? fromVersion.CreatedAt;
        var toDate = (toVersion.PublishedAt ?? toVersion.CreatedAt).AddSeconds(5);

        var changeEvents = await dbContext.Database.SqlQueryRaw<ChangeEventRow>(
            @"SELECT id AS ""Id"", plant_id AS ""PlantId"", change_type::text AS ""ChangeType"",
                     ST_Y(old_location::geometry) AS ""OldLat"", ST_X(old_location::geometry) AS ""OldLon"",
                     ST_Y(new_location::geometry) AS ""NewLat"", ST_X(new_location::geometry) AS ""NewLon"",
                     old_row_index AS ""OldRow"", new_row_index AS ""NewRow"",
                     old_column_index AS ""OldCol"", new_column_index AS ""NewCol"",
                     notes AS ""Notes"", created_at AS ""CreatedAt""
              FROM plant.plant_change_events
              WHERE farm_id = {0} AND created_at >= {1} AND created_at <= {2}
              ORDER BY created_at ASC",
            request.FarmId, fromDate, toDate)
            .ToListAsync(cancellationToken);

        var changes = new List<ComparePoleChangeItemDto>();
        var createdCount = 0;
        var relocatedCount = 0;
        var replacedCount = 0;
        var unchangedCount = 0;

        foreach (var c in toCandidates)
        {
            var row = c.RowIndex ?? 0;
            var col = c.ColumnIndex ?? 0;
            var newPos = new GeoCoordinateDto(c.Lat, c.Lon);

            switch (c.ReviewDecision)
            {
                case "CreateNew":
                    createdCount++;
                    changes.Add(new ComparePoleChangeItemDto(
                        PlantId: c.CandidateId,
                        PlantCode: $"P-{request.FarmId.ToString()[..4]}-{row:D3}-{col:D3}",
                        ChangeType: "CREATED",
                        Row: row,
                        Col: col,
                        NewPosition: newPos,
                        OldPosition: null,
                        DistanceMovedM: null,
                        Notes: "Trụ cây mới được trồng thêm trong đợt bay này"));
                    break;

                case "Relocate":
                    relocatedCount++;
                    // Tìm tọa độ cũ từ change event hoặc fromCandidates
                    var evt = changeEvents.FirstOrDefault(e => e.PlantId == c.TargetPlantId && e.OldLat.HasValue);
                    GeoCoordinateDto? oldPos = null;
                    double? distM = null;

                    if (evt?.OldLat != null && evt?.OldLon != null)
                    {
                        oldPos = new GeoCoordinateDto(evt.OldLat.Value, evt.OldLon.Value);
                        distM = Math.Round(CalculateDistanceM(evt.OldLat.Value, evt.OldLon.Value, c.Lat, c.Lon), 2);
                    }

                    changes.Add(new ComparePoleChangeItemDto(
                        PlantId: c.TargetPlantId ?? c.CandidateId,
                        PlantCode: $"P-{request.FarmId.ToString()[..4]}-{row:D3}-{col:D3}",
                        ChangeType: "RELOCATED",
                        Row: row,
                        Col: col,
                        NewPosition: newPos,
                        OldPosition: oldPos,
                        DistanceMovedM: distM,
                        Notes: distM.HasValue ? $"Nắn chỉnh vị trí dịch chuyển {distM.Value}m" : "Nắn chỉnh vị trí trụ"));
                    break;

                case "Replace":
                    replacedCount++;
                    changes.Add(new ComparePoleChangeItemDto(
                        PlantId: c.CandidateId,
                        PlantCode: $"P-{request.FarmId.ToString()[..4]}-{row:D3}-{col:D3}",
                        ChangeType: "REPLACED",
                        Row: row,
                        Col: col,
                        NewPosition: newPos,
                        OldPosition: null,
                        DistanceMovedM: null,
                        Notes: "Trụ cũ bị hư hỏng/chết được thay thế bằng trụ mới"));
                    break;

                default: // MatchExisting
                    unchangedCount++;
                    break;
            }
        }

        // 4. Tìm các cây bị xóa/nhổ bỏ (Retire)
        var removedEvts = changeEvents.Where(e => e.ChangeType == "RemovedPlant").ToList();
        var removedCount = removedEvts.Count;

        foreach (var r in removedEvts)
        {
            changes.Add(new ComparePoleChangeItemDto(
                PlantId: r.PlantId,
                PlantCode: $"P-{request.FarmId.ToString()[..4]}-{r.OldRow ?? 0:D3}-{r.OldCol ?? 0:D3}",
                ChangeType: "REMOVED",
                Row: r.OldRow ?? 0,
                Col: r.OldCol ?? 0,
                NewPosition: null,
                OldPosition: (r.OldLat.HasValue && r.OldLon.HasValue) ? new GeoCoordinateDto(r.OldLat.Value, r.OldLon.Value) : null,
                DistanceMovedM: null,
                Notes: r.Notes ?? "Trụ cây đã bị nhổ bỏ hoặc phá hủy"));
        }

        var summary = new CompareSummaryDto(
            PolesBefore: polesBefore,
            PolesAfter: polesAfter,
            NetChange: polesAfter - polesBefore,
            CreatedCount: createdCount,
            RemovedCount: removedCount,
            RelocatedCount: relocatedCount,
            ReplacedCount: replacedCount,
            UnchangedCount: unchangedCount);

        var response = new CompareFarmBaseMapsResponse(
            FarmId: request.FarmId,
            FromVersionNumber: fromVersion.VersionNumber,
            FromVersionId: fromVersion.Id,
            FromVersionDate: fromVersion.PublishedAt ?? fromVersion.CreatedAt,
            ToVersionNumber: toVersion.VersionNumber,
            ToVersionId: toVersion.Id,
            ToVersionDate: toVersion.PublishedAt ?? toVersion.CreatedAt,
            Summary: summary,
            Changes: changes);

        return Result.Success(response);
    }

    private static double CalculateDistanceM(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * r * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

