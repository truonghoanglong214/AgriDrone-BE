using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.CreateUploadSession;

public sealed record CreateUploadSessionCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    Guid OperationId,
    string FileName,
    string MimeType,
    MediaType MediaType,
    long FileSizeBytes,
    string Sha256Checksum,
    uint ExpectedMissionVersion)
    : IRequest<Result<CreateUploadSessionResult>>;