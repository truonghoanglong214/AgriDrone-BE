using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AgriDrone.Modules.Missions.Domain.Missions;
namespace AgriDrone.Modules.Missions.Infrastructure.Repositories;

internal sealed class DroneRepository(
    MissionsDbContext dbContext) : IDroneRepository, AgriDrone.Modules.Missions.Application.Abstractions.Missions.IDroneRegistryUniquenessQuery
{
    public Task<Drone?> GetByIdAsync(
        Guid droneId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Drones.SingleOrDefaultAsync(
            drone =>
                drone.Id == droneId &&
                drone.DeletedAt == null,
            cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Drones.AnyAsync(
            drone =>
                drone.Code == code,
            cancellationToken);
    }

    public Task<bool> SerialNumberExistsAsync(
        string serialNumber,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Drones.AnyAsync(
            drone =>
                drone.SerialNumber == serialNumber,
            cancellationToken);
    }

    public Task<bool> RegistrationNumberExistsAsync(
        string registrationNumber,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Drones.AnyAsync(
            drone =>
                drone.RegistrationNumber == registrationNumber,
            cancellationToken);
    }

    public Task<bool> SerialNumberUsedByAnotherDroneAsync(
        string serialNumber,
        Guid droneId,
        CancellationToken cancellationToken = default) =>
        dbContext.Drones.AnyAsync(
            drone => drone.Id != droneId && drone.SerialNumber == serialNumber,
            cancellationToken);

    public Task<bool> RegistrationNumberUsedByAnotherDroneAsync(
        string registrationNumber,
        Guid droneId,
        CancellationToken cancellationToken = default) =>
        dbContext.Drones.AnyAsync(
            drone => drone.Id != droneId && drone.RegistrationNumber == registrationNumber,
            cancellationToken);

    public Task<bool> HasBlockingMissionAsync(
    Guid droneId,
    CancellationToken cancellationToken = default)
    {
        return dbContext.DroneMissions.AnyAsync(
            mission =>
                mission.DroneId == droneId &&
                (mission.Status == MissionStatus.Draft ||
                 mission.Status == MissionStatus.Scheduled ||
                 mission.Status == MissionStatus.InFlight),
            cancellationToken);
    }

    public void Add(Drone drone)
    {
        dbContext.Drones.Add(drone);
    }
}
