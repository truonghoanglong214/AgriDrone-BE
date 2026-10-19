using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionPlantGpsStore(MissionsDbContext db) : IMissionPlantGpsStore
{
    public async Task<bool> ReplaceAsync(Guid tenantId, Guid farmId, Guid missionId,
        IReadOnlyList<MissionPlantGpsRow> rows, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var missionExists = await db.DroneMissions.AsNoTracking().AnyAsync(
            mission => mission.Id == missionId && mission.FarmId == farmId &&
                mission.TenantId == tenantId, cancellationToken);
        if (!missionExists)
            return false;

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM mission.mission_plant_gps WHERE mission_id = {missionId}",
            cancellationToken);

        foreach (var row in rows)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO mission.mission_plant_gps (mission_id, tree_id, row_number, col_number, latitude, longitude, confidence, status, source) VALUES ({missionId}, {row.TreeId}, {row.Row}, {row.Col}, {row.Latitude}, {row.Longitude}, {row.Confidence}, {row.Status}, {row.Source})",
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
