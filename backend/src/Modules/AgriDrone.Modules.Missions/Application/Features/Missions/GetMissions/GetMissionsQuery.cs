using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetMissions;

public sealed record GetMissionsQuery(
    Guid FarmId,
    int PageNumber,
    int PageSize,
    Guid? ZoneId,
    Guid? DroneId,
    MissionType? MissionType,
    MissionStatus? Status,
    string? Search)
    : IRequest<Result<PagedResult<MissionListItemResponse>>>;
