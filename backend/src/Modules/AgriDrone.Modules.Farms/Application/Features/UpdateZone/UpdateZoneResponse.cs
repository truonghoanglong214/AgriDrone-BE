using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;
namespace AgriDrone.Modules.Farms.Application.Features.UpdateZone;

public sealed record UpdateZoneResponse(
    Guid ZoneId,
    Guid FarmId,
    string Code,
    string Name,
    Polygon? Boundary,
    decimal? AreaHectares,
    GeneralStatus Status,
    long Version,
    DateTimeOffset UpdatedAt);
