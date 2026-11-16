using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.ApproveInternalBaseMap;

public sealed record ApproveInternalBaseMapCommand(
    Guid FarmId,
    Guid BaseMapVersionId) : IRequest<Result<Guid>>;
