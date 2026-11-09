using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Application.Features.GetPlantLifecycleHistory;

internal sealed class GetPlantLifecycleHistoryQueryHandler(
    PlantsDbContext dbContext)
    : IRequestHandler<GetPlantLifecycleHistoryQuery, Result<PlantLifecycleHistoryResponse>>
{
    private sealed record PlantRow(
        Guid Id,
        Guid FarmId,
        string PlantCode,
        int? RowIndex,
        int? ColumnIndex,
        double? Lat,
        double? Lon,
        string LifecycleStatus,
        Guid? CurrentMapVersionId,
        DateTimeOffset? MappedAt,
        DateTimeOffset CreatedAt);

    private sealed record ChangeEventRow(
        Guid Id,
        string ChangeType,
        string Source,
        double? OldLat,
        double? OldLon,
        double? NewLat,
        double? NewLon,
        int? OldRow,
        int? NewRow,
        int? OldCol,
        int? NewCol,
        string? Notes,
        Guid? MissionId,
        string? MissionCode,
        Guid? CreatedBy,
        DateTimeOffset CreatedAt);

    public async Task<Result<PlantLifecycleHistoryResponse>> Handle(
        GetPlantLifecycleHistoryQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Lấy thông tin cây hiện tại
        var plant = await dbContext.Database.SqlQueryRaw<PlantRow>(
            @"SELECT id AS ""Id"", farm_id AS ""FarmId"", plant_code AS ""PlantCode"",
                     row_index AS ""RowIndex"", column_index AS ""ColumnIndex"",
                     ST_Y(location::geometry) AS ""Lat"", ST_X(location::geometry) AS ""Lon"",
                     lifecycle_status::text AS ""LifecycleStatus"",
                     farm_base_map_version_id AS ""CurrentMapVersionId"",
                     mapped_at AS ""MappedAt"", created_at AS ""CreatedAt""
              FROM plant.plants
              WHERE id = {0} AND farm_id = {1}",
            request.PlantId, request.FarmId)
            .FirstOrDefaultAsync(cancellationToken);

        if (plant is null)
        {
            return Result.Failure<PlantLifecycleHistoryResponse>(
                AppError.NotFound("Plant.NotFound", $"Plant {request.PlantId} not found in farm {request.FarmId}."));
        }

        // 2. Lấy toàn bộ sự kiện biến động (events) của cây này
        var events = await dbContext.Database.SqlQueryRaw<ChangeEventRow>(
            @"SELECT e.id AS ""Id"", e.change_type::text AS ""ChangeType"", e.source::text AS ""Source"",
                     ST_Y(e.old_location::geometry) AS ""OldLat"", ST_X(e.old_location::geometry) AS ""OldLon"",
                     ST_Y(e.new_location::geometry) AS ""NewLat"", ST_X(e.new_location::geometry) AS ""NewLon"",
                     e.old_row_index AS ""OldRow"", e.new_row_index AS ""NewRow"",
                     e.old_column_index AS ""OldCol"", e.new_column_index AS ""NewCol"",
                     e.notes AS ""Notes"", e.mission_id AS ""MissionId"",
                     m.mission_code AS ""MissionCode"",
                     e.created_by AS ""CreatedBy"", e.created_at AS ""CreatedAt""
              FROM plant.plant_change_events e
              LEFT JOIN mission.drone_missions m ON m.id = e.mission_id
              WHERE e.plant_id = {0} AND e.farm_id = {1}
              ORDER BY e.created_at ASC",
            request.PlantId, request.FarmId)
            .ToListAsync(cancellationToken);

        var timeline = new List<PlantLifecycleEventItemDto>();

        foreach (var e in events)
        {
            GeoPointDto? oldPos = (e.OldLat.HasValue && e.OldLon.HasValue) ? new GeoPointDto(e.OldLat.Value, e.OldLon.Value) : null;
            GeoPointDto? newPos = (e.NewLat.HasValue && e.NewLon.HasValue) ? new GeoPointDto(e.NewLat.Value, e.NewLon.Value) : null;

            var summary = GenerateHumanReadableSummary(e);

            timeline.Add(new PlantLifecycleEventItemDto(
                EventId: e.Id,
                ChangeType: e.ChangeType,
                Source: e.Source,
                Summary: summary,
                OldPosition: oldPos,
                NewPosition: newPos,
                OldRow: e.OldRow,
                NewRow: e.NewRow,
                OldCol: e.OldCol,
                NewCol: e.NewCol,
                Notes: e.Notes,
                MissionId: e.MissionId,
                MissionCode: e.MissionCode,
                CreatedBy: e.CreatedBy,
                CreatedAt: e.CreatedAt));
        }

        // Nếu chưa có event nào trong bảng plant_change_events, tạo 1 event ban đầu từ ngày trồng/khởi tạo
        if (timeline.Count == 0)
        {
            timeline.Add(new PlantLifecycleEventItemDto(
                EventId: Guid.NewGuid(),
                ChangeType: "INITIAL_REGISTRATION",
                Source: "SYSTEM",
                Summary: $"Trụ cây được định danh lần đầu tại Hàng {plant.RowIndex ?? 0}, Cột {plant.ColumnIndex ?? 0}",
                OldPosition: null,
                NewPosition: (plant.Lat.HasValue && plant.Lon.HasValue) ? new GeoPointDto(plant.Lat.Value, plant.Lon.Value) : null,
                OldRow: null,
                NewRow: plant.RowIndex,
                OldCol: null,
                NewCol: plant.ColumnIndex,
                Notes: "Khởi tạo từ bản đồ cơ sở",
                MissionId: null,
                MissionCode: null,
                CreatedBy: null,
                CreatedAt: plant.MappedAt ?? plant.CreatedAt));
        }

        var response = new PlantLifecycleHistoryResponse(
            PlantId: plant.Id,
            FarmId: plant.FarmId,
            PlantCode: plant.PlantCode,
            CurrentLocation: (plant.Lat.HasValue && plant.Lon.HasValue) ? new GeoPointDto(plant.Lat.Value, plant.Lon.Value) : null,
            CurrentRow: plant.RowIndex,
            CurrentCol: plant.ColumnIndex,
            LifecycleStatus: plant.LifecycleStatus,
            CurrentMapVersionId: plant.CurrentMapVersionId,
            MappedAt: plant.MappedAt,
            CreatedAt: plant.CreatedAt,
            Timeline: timeline);

        return Result.Success(response);
    }

    private static string GenerateHumanReadableSummary(ChangeEventRow e)
    {
        return e.ChangeType switch
        {
            "NewPlant" => $"Trụ cây mới được thêm vào bản đồ tại Hàng {e.NewRow ?? 0}, Cột {e.NewCol ?? 0}",
            "MappingDifference" when e.OldLat.HasValue && e.NewLat.HasValue =>
                $"Nắn chỉnh tọa độ trụ từ (Lat: {e.OldLat:F6}, Lon: {e.OldLon:F6}) sang (Lat: {e.NewLat:F6}, Lon: {e.NewLon:F6})",
            "RemovedPlant" => "Trụ cây đã bị nhổ bỏ hoặc phá hủy khỏi quy hoạch vườn",
            "DeadPlant" => "Trụ cây được ghi nhận đã chết",
            "MissingPlant" => "Trụ cây không còn xuất hiện trong đợt quét khảo sát mới",
            _ => !string.IsNullOrWhiteSpace(e.Notes) ? e.Notes : $"Cập nhật trạng thái cây: {e.ChangeType}"
        };
    }
}

