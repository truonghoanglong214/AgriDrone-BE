using AgriDrone.Modules.Missions.Application
    .Features.Missions;

using AgriDrone.Modules.Missions.Application.Features.Missions.GetMissions;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Missions.Application.Abstractions;

internal interface IMissionQueries
{
    Task<PagedResult<MissionListItemResponse>> GetPageAsync(
        Guid tenantId,
        GetMissionsQuery request,
        CancellationToken cancellationToken = default);

    Task<MissionResponse?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        CancellationToken cancellationToken = default);
}
