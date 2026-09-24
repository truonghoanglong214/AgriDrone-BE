namespace AgriDrone.Modules.Missions.Domain.Drones;

public interface IDroneRepository
{
    Task<Drone?> GetByIdAsync(
        Guid droneId,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> SerialNumberExistsAsync(
        string serialNumber,
        CancellationToken cancellationToken = default);

    Task<bool> RegistrationNumberExistsAsync(
        string registrationNumber,
        CancellationToken cancellationToken = default);

    void Add(Drone drone);
}
