using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetActivePreflightChecklist;

public sealed record GetActivePreflightChecklistQuery(Guid FarmId, Guid MissionId)
    : IRequest<Result<ActivePreflightChecklistResult>>;
