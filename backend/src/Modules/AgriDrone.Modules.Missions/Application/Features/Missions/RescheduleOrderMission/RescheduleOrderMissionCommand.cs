using AgriDrone.SharedKernel.Application;
using AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.RescheduleOrderMission;

public sealed record RescheduleOrderMissionCommand(
    Guid FarmId, Guid MissionId, uint ExpectedVersion,
    DateOnly ScheduledDate,
    Guid? ReplacementDroneId = null)
    : IRequest<Result<MissionResponse>>
{
    public RescheduleOrderMissionCommand(
        Guid farmId, Guid missionId, uint expectedVersion,
        DateTimeOffset startAt, DateTimeOffset endAt,
        Guid? replacementDroneId = null)
        : this(farmId, missionId, expectedVersion,
            MissionScheduleWindow.DateAtOperationsLocation(startAt),
            replacementDroneId)
    {
    }
}
