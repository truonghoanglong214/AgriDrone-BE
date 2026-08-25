using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class DroneQueries(
    MissionsDbContext dbContext) : IDroneQueries
{
    public async Task<IReadOnlyList<AvailableDroneResponse>>
        GetAvailableAsync(
            Guid tenantId,
            DateTimeOffset startAt,
            DateTimeOffset endAt,
            CancellationToken cancellationToken = default)
    {
        var startDate =
            DateOnly.FromDateTime(startAt.UtcDateTime);

        var endDate =
            DateOnly.FromDateTime(endAt.UtcDateTime);

        return await dbContext.Drones
            .AsNoTracking()
            .Where(drone =>
                drone.TenantId == tenantId &&
                drone.DeletedAt == null &&
                drone.Status == DroneStatus.Available &&

                (!drone.RegistrationDate.HasValue ||
                 drone.RegistrationDate.Value <= startDate) &&

                (!drone.RegistrationExpiryDate.HasValue ||
                 drone.RegistrationExpiryDate.Value >= endDate) &&

                (!drone.NextMaintenanceAt.HasValue ||
                 drone.NextMaintenanceAt.Value >= endAt) &&

                !dbContext.DroneMissions.Any(mission =>
                    mission.TenantId == tenantId &&
                    mission.DroneId == drone.Id &&
                    (mission.Status == MissionStatus.Ready ||
                     mission.Status == MissionStatus.Flying) &&
                    mission.ScheduledAt.HasValue &&
                    mission.ScheduledEndAt.HasValue &&
                    mission.ScheduledAt.Value < endAt &&
                    mission.ScheduledEndAt.Value > startAt))
            .OrderBy(drone => drone.Code)
            .Select(drone => new AvailableDroneResponse(
                drone.Id,
                drone.Code,
                drone.Name,
                drone.Model,
                drone.Manufacturer,
                drone.Specifications,
                drone.RegistrationNumber,
                drone.RegistrationExpiryDate,
                drone.WeightKg,
                drone.Status,
                drone.NextMaintenanceAt))
            .ToListAsync(cancellationToken);
    }
}