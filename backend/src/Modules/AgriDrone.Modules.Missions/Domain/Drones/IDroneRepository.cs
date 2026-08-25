namespace AgriDrone.Modules.Missions.Domain.Drones;

public interface IDroneRepository
{
    Task<Drone?> GetByIdAsync(
        Guid droneId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> SerialNumberExistsAsync(
        Guid tenantId,
        string serialNumber,
        CancellationToken cancellationToken = default);

    Task<bool> RegistrationNumberExistsAsync(
        Guid tenantId,
        string registrationNumber,
        CancellationToken cancellationToken = default);

    void Add(Drone drone);
}