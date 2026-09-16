using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDownloadUrl;

public sealed record GetMissionMediaDownloadUrlQuery(Guid FarmId, Guid MissionId, Guid MediaId)
    : IRequest<Result<MissionMediaDownloadResponse>>;
