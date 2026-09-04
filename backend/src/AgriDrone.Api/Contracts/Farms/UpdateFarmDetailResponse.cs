using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Api.Contracts.Farms;

public sealed record UpdateFarmDetailResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string? Address,
    GeoJsonPolygonResponse? Boundary,
    GeoJsonPointResponse? CenterPoint,
    decimal? AreaHectares,
    GeneralStatus Status,
    DateTimeOffset UpdatedAt,
    long Version);
