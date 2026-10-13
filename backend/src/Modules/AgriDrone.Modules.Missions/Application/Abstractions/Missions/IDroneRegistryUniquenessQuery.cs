namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal interface IDroneRegistryUniquenessQuery
{
    Task<bool> SerialNumberUsedByAnotherDroneAsync(
        string serialNumber,
        Guid droneId,
        CancellationToken cancellationToken = default);

    Task<bool> RegistrationNumberUsedByAnotherDroneAsync(
        string registrationNumber,
        Guid droneId,
        CancellationToken cancellationToken = default);
}
