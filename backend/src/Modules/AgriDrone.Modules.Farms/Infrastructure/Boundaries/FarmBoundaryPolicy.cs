using AgriDrone.Modules.Farms.Application.Abstractions.Boundaries;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Farms;
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

        var exteriorRing = boundary.ExteriorRing;
        var coords = exteriorRing.Coordinates;
        if (coords.Length < 2)
        {
            return 0.0;
        }

        var latRad = point.Y * Math.PI / 180.0;
        var metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(latRad);

        var minDistanceSquared = double.MaxValue;

        for (var i = 0; i < coords.Length - 1; i++)
        {
            var x1 = (coords[i].X - point.X) * metersPerDegreeLongitude;
            var y1 = (coords[i].Y - point.Y) * MetersPerDegreeLatitude;
            var x2 = (coords[i + 1].X - point.X) * metersPerDegreeLongitude;
            var y2 = (coords[i + 1].Y - point.Y) * MetersPerDegreeLatitude;

            var dx = x2 - x1;
            var dy = y2 - y1;
            var segmentLengthSquared = (dx * dx) + (dy * dy);

            double distanceSquared;
            if (segmentLengthSquared <= double.Epsilon)
            {
                distanceSquared = (x1 * x1) + (y1 * y1);
            }
            else
            {
                var t = ((-x1 * dx) - (y1 * dy)) / segmentLengthSquared;
                var tClamped = Math.Clamp(t, 0.0, 1.0);

                var closestX = x1 + (tClamped * dx);
                var closestY = y1 + (tClamped * dy);

                distanceSquared = (closestX * closestX) + (closestY * closestY);
            }

            if (distanceSquared < minDistanceSquared)
            {
                minDistanceSquared = distanceSquared;
            }
        }

        return Math.Sqrt(minDistanceSquared);
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
            return Result.Failure(FarmError.UnmappedFarmBoundaryRequiresCaptureSession());
        }

        var effectiveBoundary = approvedBoundary.Geometry;

        var containmentResult = ValidateZoneWithinBoundary(
            zoneBoundary,
            effectiveBoundary);

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
