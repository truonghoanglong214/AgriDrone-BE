using AgriDrone.Modules.Missions.Application
    .Features.Missions;

namespace AgriDrone.Modules.Missions.Application.Abstractions;

internal interface IMissionQueries
{
    Task<MissionResponse?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        CancellationToken cancellationToken = default);
}
