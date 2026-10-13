using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionUploadReadiness;

public sealed record GetMissionUploadReadinessQuery(
    Guid FarmId,
    Guid MissionId)
    : IRequest<Result<MissionUploadReadinessResult>>;