using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.ScheduleMission;

public sealed record ScheduleMissionCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    DateTimeOffset ScheduledAt,
    DateTimeOffset ScheduledEndAt,
    uint ExpectedVersion)
    : IRequest<Result<MissionResponse>>;
