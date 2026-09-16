using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application
    .Features.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetMissions;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class MissionQueries(
    MissionsDbContext dbContext)
    : IMissionQueries
{
    public Task<PagedResult<MissionListItemResponse>> GetPageAsync(
        Guid tenantId, GetMissionsQuery request, CancellationToken cancellationToken = default)
    {
        var query = dbContext.DroneMissions.AsNoTracking()
            .Where(mission => mission.TenantId == tenantId && mission.FarmId == request.FarmId);

        if (request.ZoneId.HasValue)
            query = query.Where(mission => mission.ZoneId == request.ZoneId.Value);
        if (request.DroneId.HasValue)
            query = query.Where(mission => mission.DroneId == request.DroneId.Value);
        if (request.MissionType.HasValue)
            query = query.Where(mission => mission.MissionType == request.MissionType.Value);
        if (request.Status.HasValue)
            query = query.Where(mission => mission.Status == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Treat user input as a literal substring, including LIKE wildcard characters.
            var search = request.Search.Trim()
                .Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            query = query.Where(mission => EF.Functions.ILike(
                mission.MissionCode, $"%{search}%", "\\"));
        }

        return query.OrderByDescending(mission => mission.CreatedAt).ThenByDescending(mission => mission.Id)
            .Select(mission => new MissionListItemResponse(
                mission.Id, mission.FarmId, mission.ZoneId, mission.DroneId,
                mission.MissionCode, mission.MissionType, mission.Status, mission.ProcessingStatus,
                mission.ScheduledAt, mission.ScheduledEndAt, mission.StartedAt, mission.EndedAt,
                mission.Version, mission.CreatedAt))
            .ToPagedResultAsync(new PagedRequest(request.PageNumber, request.PageSize), cancellationToken);
    }

    public async Task<MissionResponse?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        CancellationToken cancellationToken = default)
    {
        var mission = await dbContext.DroneMissions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.Id == missionId &&
                    item.TenantId == tenantId &&
                    item.FarmId == farmId,
                cancellationToken);

        return mission is null
            ? null
            : MissionResponseMapper.Map(mission);
    }
}
