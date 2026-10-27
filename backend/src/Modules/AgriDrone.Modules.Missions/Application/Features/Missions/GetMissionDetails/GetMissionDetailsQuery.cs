using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.GetMissionDetails;

public sealed record GetMissionDetailsQuery(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId)
    : IRequest<Result<MissionResponse>>;
