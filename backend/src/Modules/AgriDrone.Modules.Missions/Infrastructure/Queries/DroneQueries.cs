using AgriDrone.Modules.Missions.Application
    .Abstractions;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneDetails;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDrones;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions
    .Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure
    .Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions
    .Infrastructure.Queries;

internal sealed class DroneQueries(
    MissionsDbContext dbContext)
    : IDroneQueries
{
    public Task<PagedResult<DroneListItemResponse>>
        GetPageAsync(
            Guid tenantId,
            PagedRequest pageRequest,
            DroneStatus? status,
            string? search,
            CancellationToken cancellationToken = default)
    {
        var query = dbContext.Drones
            .AsNoTracking()
            .Where(drone =>
                drone.TenantId == tenantId &&
                drone.DeletedAt == null);

        if (status.HasValue)
        {
            query = query.Where(
                drone => drone.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern =
                $"%{search.Trim()}%";

            query = query.Where(drone =>
                EF.Functions.ILike(
                    drone.Code,
                    searchPattern) ||
                EF.Functions.ILike(
                    drone.Name,
                    searchPattern) ||
                (drone.Model != null &&
                 EF.Functions.ILike(
                     drone.Model,
                     searchPattern)) ||
                (drone.Manufacturer != null &&
                 EF.Functions.ILike(
                     drone.Manufacturer,
                     searchPattern)) ||
                (drone.SerialNumber != null &&
                 EF.Functions.ILike(
                     drone.SerialNumber,
                     searchPattern)));
        }

        return query
            .OrderByDescending(
                drone => drone.CreatedAt)
            .ThenByDescending(
                drone => drone.Id)
            .Select(drone =>
                new DroneListItemResponse(
                    drone.Id,
                    drone.TenantId,
                    drone.Code,
                    drone.Name,
                    drone.Model,
                    drone.Manufacturer,
                    drone.SerialNumber,
                    drone.Status,
                    drone.NextMaintenanceAt,
                    drone.CreatedAt,
                    drone.UpdatedAt))
            .ToPagedResultAsync(
                pageRequest,
                cancellationToken);
    }

    public Task<PagedResult<DroneListItemResponse>>
    GetSystemPageAsync(
        Guid? tenantId,
        PagedRequest pageRequest,
        DroneStatus? status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Drones
            .AsNoTracking()
            .Where(drone => drone.DeletedAt == null);

        if (tenantId.HasValue)
        {
            query = query.Where(
                drone =>
                    drone.TenantId == tenantId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(
                drone => drone.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern =
                $"%{search.Trim()}%";

            query = query.Where(drone =>
                EF.Functions.ILike(
                    drone.Code,
                    searchPattern) ||
                EF.Functions.ILike(
                    drone.Name,
                    searchPattern) ||
                (drone.Model != null &&
                 EF.Functions.ILike(
                     drone.Model,
                     searchPattern)) ||
                (drone.Manufacturer != null &&
                 EF.Functions.ILike(
                     drone.Manufacturer,
                     searchPattern)) ||
                (drone.SerialNumber != null &&
                 EF.Functions.ILike(
                     drone.SerialNumber,
                     searchPattern)));
        }

        return query
            .OrderByDescending(
                drone => drone.CreatedAt)
            .ThenByDescending(
                drone => drone.Id)
            .Select(drone =>
                new DroneListItemResponse(
                    drone.Id,
                    drone.TenantId,
                    drone.Code,
                    drone.Name,
                    drone.Model,
                    drone.Manufacturer,
                    drone.SerialNumber,
                    drone.Status,
                    drone.NextMaintenanceAt,
                    drone.CreatedAt,
                    drone.UpdatedAt))
            .ToPagedResultAsync(
                pageRequest,
                cancellationToken);
    }

    public Task<DroneDetailsResponse?>
    GetSystemDetailsAsync(
        Guid droneId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Drones
            .AsNoTracking()
            .Where(drone =>
                drone.Id == droneId &&
                drone.DeletedAt == null)
            .Select(drone =>
                new DroneDetailsResponse(
                    drone.Id,
                    drone.TenantId,
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
                    drone.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<DroneDetailsResponse?>
    GetDetailsAsync(
        Guid droneId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Drones
            .AsNoTracking()
            .Where(drone =>
                drone.Id == droneId &&
                drone.TenantId == tenantId &&
                drone.DeletedAt == null)
                .Select(drone =>
                new DroneDetailsResponse(
                    drone.Id,
                    drone.TenantId,
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
                    drone.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<
        IReadOnlyList<AvailableDroneResponse>>
        GetAvailableAsync(
            Guid tenantId,
            DateTimeOffset startAt,
            DateTimeOffset endAt,
            CancellationToken cancellationToken = default)
    {
        var startDate =
            DateOnly.FromDateTime(
                startAt.UtcDateTime);

        var endDate =
            DateOnly.FromDateTime(
                endAt.UtcDateTime);

        return await dbContext.Drones
            .AsNoTracking()
            .Where(drone =>
                drone.TenantId == tenantId &&
                drone.DeletedAt == null &&
                drone.Status ==
                    DroneStatus.Available &&

                (!drone.RegistrationDate.HasValue ||
                 drone.RegistrationDate.Value <=
                 startDate) &&

                (!drone.RegistrationExpiryDate
                    .HasValue ||
                 drone.RegistrationExpiryDate.Value >=
                 endDate) &&

                (!drone.NextMaintenanceAt.HasValue ||
                 drone.NextMaintenanceAt.Value >=
                 endAt) &&

                !dbContext.DroneMissions.Any(
                    mission =>
                        mission.TenantId ==
                            tenantId &&
                        mission.DroneId ==
                            drone.Id &&
                        (mission.Status ==
                             MissionStatus.Scheduled ||
                         mission.Status ==
                             MissionStatus.InFlight) &&
                        mission.ScheduledAt.HasValue &&
                        mission.ScheduledEndAt
                            .HasValue &&
                        mission.ScheduledAt.Value <
                            endAt &&
                        mission.ScheduledEndAt.Value >
                            startAt))
            .OrderBy(drone => drone.Code)
            .Select(drone =>
                new AvailableDroneResponse(
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