using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDetails;

public sealed record GetMissionMediaDetailsQuery(Guid FarmId, Guid MissionId, Guid MediaId)
    : IRequest<Result<MissionMediaResponse>>;
