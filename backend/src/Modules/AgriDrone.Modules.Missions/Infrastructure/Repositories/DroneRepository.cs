using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Repositories;

internal sealed class DroneRepository(
    MissionsDbContext dbContext) : IDroneRepository
{
    public Task<Drone?> GetByIdAsync(
        Guid droneId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Drones.SingleOrDefaultAsync(
            drone =>
                drone.Id == droneId &&
                drone.TenantId == tenantId &&
                drone.DeletedAt == null,
            cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string code,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Drones.AnyAsync(
            drone =>
                drone.TenantId == tenantId &&
                drone.Code == code,
            cancellationToken);
    }

    public Task<bool> SerialNumberExistsAsync(
        Guid tenantId,
        string serialNumber,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Drones.AnyAsync(
            drone =>
                drone.TenantId == tenantId &&
                drone.SerialNumber == serialNumber,
            cancellationToken);
    }

    public Task<bool> RegistrationNumberExistsAsync(
        Guid tenantId,
        string registrationNumber,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Drones.AnyAsync(
            drone =>
                drone.TenantId == tenantId &&
                drone.RegistrationNumber == registrationNumber,
            cancellationToken);
    }

    public void Add(Drone drone)
    {
        dbContext.Drones.Add(drone);
    }
}