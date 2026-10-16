using AgriDrone.Modules.Missions.Application.Features.Media.CompleteUploadSession;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.UploadMissionMedia;

public sealed record UploadMissionMediaCommand(
    Guid TenantId, Guid FarmId, Guid MissionId, Stream Content,
    DateTimeOffset? CapturedAt, string? FileName = null)
    : IRequest<Result<CompleteUploadSessionResult>>;
