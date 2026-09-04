using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.UpdateFarmDetail;

public sealed record UpdateFarmDetailResponse(
    Guid id,
    Guid tenantId,
    string code,
    string name,
    string? address,
    Polygon? boundary,
    Point? centerPoint,
    decimal? areaHectares,
    GeneralStatus status,
    DateTimeOffset updatedAt,
    long version);
