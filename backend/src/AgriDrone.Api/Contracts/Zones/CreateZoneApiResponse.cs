using AgriDrone.Api.Contracts.Farms;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Api.Contracts.Zones;

public sealed record CreateZoneApiResponse(
    Guid ZoneId,
    Guid FarmId,
    string Code,
    string Name,
    GeoJsonPolygonResponse? Boundary,
    decimal? AreaHectares,
    GeneralStatus Status,
    long Version,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);
