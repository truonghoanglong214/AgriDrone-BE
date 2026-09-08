using AgriDrone.SharedKernel.Application;
using MediatR;
using NetTopologySuite.Geometries;
namespace AgriDrone.Modules.Farms.Application.Features.UpdateZone;

public sealed record UpdateZoneCommand(
    Guid FarmId,
    Guid ZoneId,
    string Name,
    Polygon? Boundary,
    decimal? AreaHectares,
    long ExpectedVersion) : IRequest<Result<UpdateZoneResponse>>;
