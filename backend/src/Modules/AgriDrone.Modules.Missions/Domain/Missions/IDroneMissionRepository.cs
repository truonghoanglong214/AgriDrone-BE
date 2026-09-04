namespace AgriDrone.Modules.Missions.Domain.Missions;

public interface IDroneMissionRepository
{
    Task<DroneMission?> GetByIdAsync(
        Guid missionId,
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(
        Guid farmId,
        string missionCode,
        CancellationToken cancellationToken = default);

    void Add(DroneMission mission);
}
