using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMultipartPartUrl;

public sealed record GetMultipartPartUrlQuery(Guid TenantId, Guid FarmId,
    Guid MissionId, Guid UploadSessionId, int PartNumber)
    : IRequest<Result<GetMultipartPartUrlResult>>;
