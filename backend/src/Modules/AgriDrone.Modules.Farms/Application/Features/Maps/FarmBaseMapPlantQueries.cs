using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.Maps;

internal static class FarmBaseMapPlantQueries
{
    internal sealed record VersionCountRow(Guid VersionId, int Count);

    /// <summary>
    /// Đếm số lượng cây đang hoạt động (không tính cây đã xóa) theo từng phiên bản bản đồ,
    /// bao gồm cả các trụ MatchExisting từ phiên bản trước (plant_profile_observations)
    /// và các trụ có biến động AI gắn với phiên bản đó (plant_change_events).
    /// </summary>
    public static async Task<Dictionary<Guid, int>> GetActivePoleCountsByVersionAsync(
        FarmsDbContext dbContext,
        Guid farmId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQueryRaw<VersionCountRow>(
            @"WITH version_plants AS (
                  SELECT farm_base_map_version_id AS version_id, id AS plant_id
                  FROM plant.plants
                  WHERE farm_id = {0}
                    AND farm_base_map_version_id IS NOT NULL
                    AND lifecycle_status != 'REMOVED'
                  UNION
                  SELECT o.farm_base_map_version_id AS version_id, p.id AS plant_id
                  FROM plant.plant_profile_observations o
                  JOIN plant.plants p ON p.farm_id = o.farm_id AND p.plant_profile_id = o.plant_profile_id
                  WHERE o.farm_id = {0}
                    AND o.farm_base_map_version_id IS NOT NULL
                    AND p.lifecycle_status != 'REMOVED'
                  UNION
                  SELECT e.farm_base_map_version_id AS version_id, p.id AS plant_id
                  FROM plant.plant_change_events e
                  JOIN plant.plants p ON p.farm_id = e.farm_id AND p.id = e.plant_id
                  WHERE e.farm_id = {0}
                    AND e.farm_base_map_version_id IS NOT NULL
                    AND e.source::text = 'MISSION_AI'
                    AND p.lifecycle_status != 'REMOVED'
              )
              SELECT version_id AS ""VersionId"", COUNT(*)::int AS ""Count""
              FROM version_plants
              GROUP BY version_id",
            farmId)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.VersionId, r => r.Count);
    }

    /// <summary>
    /// Đếm số lượng sự kiện chỉnh sửa thủ công (source = MANUAL) theo từng phiên bản bản đồ.
    /// </summary>
    public static async Task<Dictionary<Guid, int>> GetManualChangeCountsByVersionAsync(
        FarmsDbContext dbContext,
        Guid farmId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQueryRaw<VersionCountRow>(
            @"SELECT farm_base_map_version_id AS ""VersionId"", COUNT(*)::int AS ""Count""
              FROM plant.plant_change_events
              WHERE farm_id = {0}
                AND farm_base_map_version_id IS NOT NULL
                AND source::text = 'MANUAL'
              GROUP BY farm_base_map_version_id",
            farmId)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.VersionId, r => r.Count);
    }
}
