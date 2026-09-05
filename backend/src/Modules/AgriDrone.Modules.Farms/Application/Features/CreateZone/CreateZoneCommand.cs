using AgriDrone.SharedKernel.Application;
using MediatR;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.CreateZone
{
    public sealed record CreateZoneCommand(
        Guid FarmId,
        string Code,
        string Name,
        Polygon? Boundary,
        decimal? AreaHectares) : IRequest<Result<CreateZoneResponse>>;
}
 
