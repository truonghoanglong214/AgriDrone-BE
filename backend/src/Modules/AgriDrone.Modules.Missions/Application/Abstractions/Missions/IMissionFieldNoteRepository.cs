using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public interface IMissionFieldNoteRepository
{
    Task<MissionFieldNote?> GetByOperationIdAsync(
        Guid tenantId, Guid farmId, Guid missionId, Guid operationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MissionFieldNote>> ListAsync(
        Guid tenantId, Guid farmId, Guid missionId,
        CancellationToken cancellationToken = default);

    void Add(MissionFieldNote note);
}
