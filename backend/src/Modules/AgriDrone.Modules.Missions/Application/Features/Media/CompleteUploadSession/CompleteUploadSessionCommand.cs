using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.CompleteUploadSession;

public sealed record CompleteUploadSessionCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    Guid UploadSessionId,
    DateTimeOffset? CapturedAt = null)
    : IRequest<Result<CompleteUploadSessionResult>>;
