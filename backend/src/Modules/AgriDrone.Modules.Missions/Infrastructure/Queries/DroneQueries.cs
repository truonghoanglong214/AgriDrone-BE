using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;
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
            DateTimeOffset startAt,
            DateTimeOffset endAt,
            CancellationToken cancellationToken = default) =>
        await GetAvailableInternalAsync(startAt, endAt, null, cancellationToken);

    public Task<IReadOnlyList<AvailableDroneResponse>> GetAvailableExcludingMissionAsync(
        DateTimeOffset startAt, DateTimeOffset endAt, Guid excludedMissionId,
        CancellationToken cancellationToken = default) =>
        GetAvailableInternalAsync(startAt, endAt, excludedMissionId, cancellationToken);

    private async Task<IReadOnlyList<AvailableDroneResponse>> GetAvailableInternalAsync(
        DateTimeOffset startAt, DateTimeOffset endAt, Guid? excludedMissionId,
        CancellationToken cancellationToken)
    {
        var startDate =
            DateOnly.FromDateTime(startAt.UtcDateTime);

        var endDate =
            DateOnly.FromDateTime(endAt.UtcDateTime);

        return await dbContext.Drones
            .AsNoTracking()
            .Where(drone =>
                drone.DeletedAt == null &&
                drone.Status == DroneStatus.Available &&
                drone.RegistrationNumber != null &&
                drone.RegistrationDate.HasValue &&
                drone.RegistrationDate.Value <= startDate &&
                drone.RegistrationExpiryDate.HasValue &&
                drone.RegistrationExpiryDate.Value >= endDate &&

                (!drone.NextMaintenanceAt.HasValue ||
                 drone.NextMaintenanceAt.Value >= endAt) &&

                !dbContext.DroneMissions.Any(mission =>
                    mission.DroneId == drone.Id &&
                    (!excludedMissionId.HasValue || mission.Id != excludedMissionId.Value) &&
                    (mission.Status == MissionStatus.Scheduled ||
                     mission.Status == MissionStatus.InFlight) &&
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

    public async Task<IReadOnlyList<DroneRegistryItemResponse>>
        GetRegistryAsync(
            CancellationToken cancellationToken = default)
    {
        return await dbContext.Drones
            .AsNoTracking()
            .Where(drone => drone.DeletedAt == null)
            .OrderBy(drone => drone.Code)
            .Select(drone => new DroneRegistryItemResponse(
                drone.Id,
                drone.Code,
                drone.Name,
                drone.Model,
                drone.Manufacturer,
                drone.Specifications,
                drone.SerialNumber,
                drone.RegistrationNumber,
                drone.RegistrationDate,
                drone.RegistrationExpiryDate,
                drone.WeightKg,
                drone.Status,
                drone.LastMaintenanceAt,
                drone.NextMaintenanceAt,
                drone.Notes,
                drone.CreatedAt,
                drone.UpdatedAt,
                drone.Version))
            .ToListAsync(cancellationToken);
    }
}
