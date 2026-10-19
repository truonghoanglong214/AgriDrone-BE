using AgriDrone.SharedKernel.Application;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Abstractions.Boundaries;

public enum PointBoundaryClassification
{
    WithinBoundary,
    NearBoundary,
    OutOfBoundary
}

public interface IFarmBoundaryPolicy
{
    PointBoundaryClassification ClassifyPoint(
        Point point,
        Polygon boundary,
        double nearBoundaryThresholdMeters = 5.0);

    double CalculateDistanceToBoundaryMeters(
        Point point,
        Polygon boundary);

    Result ValidateZoneWithinBoundary(
        Polygon zoneBoundary,
        Polygon farmBoundary);

    Task<Result> ValidateZoneAsync(
        Guid tenantId,
        Guid farmId,
        Polygon? zoneBoundary,
        decimal? zoneAreaHectares,
        Guid? excludingZoneId = null,
        CancellationToken cancellationToken = default);
}
