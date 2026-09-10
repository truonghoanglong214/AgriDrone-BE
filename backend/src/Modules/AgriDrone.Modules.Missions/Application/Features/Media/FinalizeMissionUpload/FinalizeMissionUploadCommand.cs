using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.FinalizeMissionUpload;

public sealed record FinalizeMissionUploadCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    uint ExpectedMissionVersion)
    : IRequest<Result<FinalizeMissionUploadResult>>;