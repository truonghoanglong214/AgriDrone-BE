using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Domain.Boundaries;

internal static class FarmBoundaryGeometryGuard
{
    public static Polygon ValidPolygon(Polygon polygon, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(polygon, parameterName);

        if (polygon.SRID != 4326 || polygon.IsEmpty || !polygon.IsValid)
        {
            throw new ArgumentException(
                "Boundary must be a non-empty valid Polygon using SRID 4326.",
                parameterName);
        }

        return (Polygon)polygon.Copy();
    }

    public static Point ValidPoint(Point point, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(point, parameterName);

        if (point.SRID != 4326 || point.IsEmpty || !point.IsValid)
        {
            throw new ArgumentException(
                "Position must be a non-empty valid Point using SRID 4326.",
                parameterName);
        }

        return (Point)point.Copy();
    }
}
