using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMedia;

public sealed record GetMissionMediaQuery(
    Guid FarmId, Guid MissionId, int PageNumber, int PageSize,
    MediaType? MediaType, MissionMediaRole? MediaRole)
    : IRequest<Result<PagedResult<MissionMediaResponse>>>;
