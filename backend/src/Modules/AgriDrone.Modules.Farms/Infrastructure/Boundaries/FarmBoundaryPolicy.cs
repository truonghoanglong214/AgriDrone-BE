using AgriDrone.Modules.Farms.Application.Abstractions.Boundaries;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedKernel.Application;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Infrastructure.Boundaries;

internal sealed class FarmBoundaryPolicy(
    IFarmBoundaryRepository boundaryRepository,
    IFarmZoneRepository zoneRepository)
    : IFarmBoundaryPolicy
{
    private const double MetersPerDegreeLatitude = 111_320.0;

    public PointBoundaryClassification ClassifyPoint(
        Point point,
        Polygon boundary,
        double nearBoundaryThresholdMeters = 5.0)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(boundary);

        var isCovered = boundary.Covers(point);
        var distanceMeters = CalculateDistanceToBoundaryMeters(point, boundary);

        if (distanceMeters <= nearBoundaryThresholdMeters)
        {
            return PointBoundaryClassification.NearBoundary;
        }

        return isCovered
            ? PointBoundaryClassification.WithinBoundary
            : PointBoundaryClassification.OutOfBoundary;
    }

    public double CalculateDistanceToBoundaryMeters(
        Point point,
        Polygon boundary)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(boundary);

        // Distance in degrees to the exterior ring of the polygon
        var exteriorRing = boundary.ExteriorRing;
        var distanceDegrees = point.Distance(exteriorRing);

        // Convert degree distance to approximate meters using average latitude
        var latRad = point.Y * Math.PI / 180.0;
        var metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(latRad);
        var averageMetersPerDegree = (MetersPerDegreeLatitude + metersPerDegreeLongitude) / 2.0;

        return distanceDegrees * averageMetersPerDegree;
    }

    public Result ValidateZoneWithinBoundary(
        Polygon zoneBoundary,
        Polygon farmBoundary)
    {
        ArgumentNullException.ThrowIfNull(zoneBoundary);
        ArgumentNullException.ThrowIfNull(farmBoundary);

        if (zoneBoundary.SRID != 4326 || zoneBoundary.IsEmpty || !zoneBoundary.IsValid)
        {
            return Result.Failure(FarmZoneError.InvalidBoundary());
        }

        if (!farmBoundary.Covers(zoneBoundary))
        {
            return Result.Failure(FarmZoneError.BoundaryOutsideFarm());
        }

        return Result.Success();
    }

    public async Task<Result> ValidateZoneAsync(
        Guid tenantId,
        Guid farmId,
        Polygon? zoneBoundary,
        decimal? zoneAreaHectares,
        Guid? excludingZoneId = null,
        CancellationToken cancellationToken = default)
    {
        if (zoneBoundary is null)
        {
            return Result.Success();
        }

        if (zoneBoundary.SRID != 4326 || zoneBoundary.IsEmpty || !zoneBoundary.IsValid)
        {
            return Result.Failure(FarmZoneError.InvalidBoundary());
        }

        if (zoneAreaHectares < 0)
        {
            return Result.Failure(FarmZoneError.InvalidArea());
        }

        var approvedBoundary = await boundaryRepository.GetCurrentApprovedAsync(
            tenantId,
            farmId,
            cancellationToken);

        if (approvedBoundary is null)
        {
            return Result.Failure(FarmError.NotFound());
        }

        var containmentResult = ValidateZoneWithinBoundary(
            zoneBoundary,
            approvedBoundary.Geometry);

        if (containmentResult.IsFailure)
        {
            return containmentResult;
        }

        if (await zoneRepository.ActiveBoundaryOverlapsAsync(
                tenantId,
                farmId,
                zoneBoundary,
                excludingZoneId,
                cancellationToken))
        {
            return Result.Failure(FarmZoneError.BoundaryOverlaps());
        }

        return Result.Success();
    }
}
