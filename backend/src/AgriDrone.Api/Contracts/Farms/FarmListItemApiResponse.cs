using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Api.Contracts.Farms;

public sealed record FarmListItemApiResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string? Address,
    GeoJsonPolygonResponse? Boundary,
    GeoJsonPointResponse? CenterPoint,
    decimal? AreaHectares,
    GeneralStatus Status,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);
