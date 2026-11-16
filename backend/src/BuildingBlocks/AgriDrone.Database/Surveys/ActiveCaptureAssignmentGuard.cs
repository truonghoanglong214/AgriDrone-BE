using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class ActiveCaptureAssignmentGuard(AgriDroneSchemaDbContext db) : IActiveCaptureAssignmentGuard
{
    public async Task<bool> HasInFlightCaptureAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        var hasInFlightAttempt = await db.Set<CaptureAttempt>()
            .AsNoTracking()
            .AnyAsync(attempt =>
                attempt.Status == CaptureAttemptStatus.InFlight &&
                db.Set<SurveyOrder>().Any(order =>
                    order.Id == attempt.OrderId &&
                    order.TenantId == tenantId &&
                    order.FarmId == farmId),
                cancellationToken);

        if (hasInFlightAttempt)
        {
            return true;
        }

        var hasInFlightMission = await db.Set<DroneMission>()
            .AsNoTracking()
            .AnyAsync(mission =>
                mission.FarmId == farmId &&
                mission.TenantId == tenantId &&
                mission.Status == MissionStatus.InFlight,
                cancellationToken);

        return hasInFlightMission;
    }
}
