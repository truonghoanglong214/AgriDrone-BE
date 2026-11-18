using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.CompareFarmBaseMaps;

public sealed record CompareFarmBaseMapsQuery(
    Guid FarmId,
    int? FromVersionNumber = null,
    int? ToVersionNumber = null,
    Guid? FromVersionId = null,
    Guid? ToVersionId = null)
    : IRequest<Result<CompareFarmBaseMapsResponse>>;

