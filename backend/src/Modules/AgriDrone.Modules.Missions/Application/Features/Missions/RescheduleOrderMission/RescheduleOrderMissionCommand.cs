using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.RescheduleOrderMission;

public sealed record RescheduleOrderMissionCommand(
    Guid FarmId, Guid MissionId, uint ExpectedVersion,
    DateTimeOffset StartAt, DateTimeOffset EndAt,
    Guid? ReplacementDroneId = null)
    : IRequest<Result<MissionResponse>>;
