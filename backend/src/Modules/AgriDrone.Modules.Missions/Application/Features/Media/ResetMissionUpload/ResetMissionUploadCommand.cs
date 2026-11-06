using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.ResetMissionUpload;

public sealed record ResetMissionUploadCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    uint? ExpectedMissionVersion = null,
    bool ResetTelemetry = false) : IRequest<Result<ResetMissionUploadResult>>;

