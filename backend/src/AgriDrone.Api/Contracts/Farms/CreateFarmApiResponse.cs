using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Api.Contracts.Farms;

public sealed record CreateFarmApiResponse(
    Guid FarmId,
    Guid TenantId,
    string Code,
    string Name,
    string? Address,
    GeoJsonPolygonResponse? Boundary,
    GeoJsonPointResponse? CenterPoint,
    decimal? AreaHectares,
    GeneralStatus Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);
