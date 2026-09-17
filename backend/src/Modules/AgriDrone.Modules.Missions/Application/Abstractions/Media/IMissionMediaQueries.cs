using AgriDrone.Modules.Missions.Application.Features.Media;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

internal interface IMissionMediaQueries
{
    Task<bool> MissionExistsAsync(
        Guid tenantId, Guid farmId, Guid missionId, CancellationToken cancellationToken);
    Task<PagedResult<MissionMediaResponse>> GetPageAsync(
        Guid tenantId, Guid farmId, Guid missionId, PagedRequest page,
        MediaType? mediaType, MissionMediaRole? mediaRole, CancellationToken cancellationToken);
    Task<MissionMediaResponse?> GetDetailsAsync(
        Guid tenantId, Guid farmId, Guid missionId, Guid mediaId, CancellationToken cancellationToken);
    Task<string?> GetDownloadSourceAsync(
        Guid tenantId, Guid farmId, Guid missionId, Guid mediaId, CancellationToken cancellationToken);
}
