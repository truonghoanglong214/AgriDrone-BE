using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.GetZonesByFarm;

public sealed record ZoneListItemResponse(
    Guid ZoneId,
    Guid FarmId,
    string Code,
    string Name,
    Polygon? Boundary,
    decimal? AreaHectares,
    GeneralStatus Status,
    long Version,
    DateTimeOffset CreatedAt);
