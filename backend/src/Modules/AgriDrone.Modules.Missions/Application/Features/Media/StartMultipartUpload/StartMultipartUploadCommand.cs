using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.StartMultipartUpload;

public sealed record StartMultipartUploadCommand(Guid TenantId, Guid FarmId,
    Guid MissionId, Guid UploadSessionId)
    : IRequest<Result<StartMultipartUploadResult>>;
