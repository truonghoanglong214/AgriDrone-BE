using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Repositories;

internal sealed class DroneRepository(
    MissionsDbContext dbContext) : IDroneRepository
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

    public void Add(Drone drone)
    {
        dbContext.Drones.Add(drone);
    }
}
