using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.GetArchivedFarms;

public sealed record ArchivedFarmResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string? Address,
    Polygon? Boundary,
    Point? CenterPoint,
    decimal? AreaHectares,
    GeneralStatus Status,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ArchivedAt,
    long Version);
