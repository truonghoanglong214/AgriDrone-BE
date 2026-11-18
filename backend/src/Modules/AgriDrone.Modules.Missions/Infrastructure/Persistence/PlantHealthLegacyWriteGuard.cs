using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal static class PlantHealthLegacyWriteGuard
{
    public static Task<bool> IsV4Async(MissionsDbContext db, Guid observationId, Guid farmId, CancellationToken token) =>
        db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM mission.mission_plant_observations o
                JOIN mission.ai_processing_jobs j ON j.id = o.ai_job_id
                WHERE o.id = {observationId} AND o.farm_id = {farmId}
                  AND j.job_type = 'DISEASE_DETECTION' AND j.parameters ? 'SourceVersion'
            ) AS "Value"
            """).SingleAsync(token);
}
