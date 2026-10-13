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

    Task<IReadOnlyList<DroneMission>> GetBySurveyOrderIdAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DroneMission>>([]);

    void Add(DroneMission mission);
}
