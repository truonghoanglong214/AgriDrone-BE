using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedKernel.Application;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Policies;

internal interface IFarmGeometryPolicy
{
    Result ValidateFarm(Polygon? boundary, Point? centerPoint, decimal? areaHectares);

    Task<Result> ValidateZoneAsync(
        Guid tenantId,
        Farm farm,
        Polygon? boundary,
        decimal? areaHectares,
        Guid? excludingZoneId = null,
        CancellationToken cancellationToken = default);
}

internal sealed class FarmGeometryPolicy(IFarmZoneRepository zoneRepository)
    : IFarmGeometryPolicy
{
    public Result ValidateFarm(
        Polygon? boundary,
        Point? centerPoint,
        decimal? areaHectares)
    {
        if (!IsValidPolygon(boundary))
        {
            return Result.Failure(FarmError.InvalidBoundary());
        }

        if (!IsValidPoint(centerPoint))
        {
            return Result.Failure(FarmError.InvalidCenterPoint());
        }

        if (areaHectares < 0)
        {
            return Result.Failure(FarmError.InvalidArea());
        }

        if (boundary is not null &&
            centerPoint is not null &&
            !boundary.Covers(centerPoint))
        {
            return Result.Failure(FarmError.CenterPointOutsideBoundary());
        }

        return Result.Success();
    }

    public async Task<Result> ValidateZoneAsync(
        Guid tenantId,
        Farm farm,
        Polygon? boundary,
        decimal? areaHectares,
        Guid? excludingZoneId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidPolygon(boundary))
        {
            return Result.Failure(FarmZoneError.InvalidBoundary());
        }

        if (areaHectares < 0 ||
            areaHectares.HasValue &&
            farm.AreaHectares.HasValue &&
            areaHectares.Value > farm.AreaHectares.Value)
        {
            return Result.Failure(FarmZoneError.InvalidArea());
        }

        if (boundary is not null &&
            farm.Boundary is not null &&
            !farm.Boundary.Covers(boundary))
        {
            return Result.Failure(FarmZoneError.BoundaryOutsideFarm());
        }

        if (boundary is not null &&
            await zoneRepository.ActiveBoundaryOverlapsAsync(
                tenantId,
                farm.Id,
                boundary,
                excludingZoneId,
                cancellationToken))
        {
            return Result.Failure(FarmZoneError.BoundaryOverlaps());
        }

        return Result.Success();
    }

    private static bool IsValidPolygon(Polygon? polygon) =>
        polygon is null ||
        polygon.SRID == 4326 && !polygon.IsEmpty && polygon.IsValid;

    private static bool IsValidPoint(Point? point) =>
        point is null ||
        point.SRID == 4326 &&
        !point.IsEmpty &&
        double.IsFinite(point.X) &&
        double.IsFinite(point.Y) &&
        point.X is >= -180 and <= 180 &&
        point.Y is >= -90 and <= 90;
}
