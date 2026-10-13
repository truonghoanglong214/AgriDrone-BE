using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.DiseaseZones;

internal static class DiseaseZoneGeometryGuard
{
    public static Polygon ValidPolygon(Polygon polygon, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(polygon, parameterName);

        if (polygon.SRID != 4326 || polygon.IsEmpty || !polygon.IsValid)
        {
            throw new ArgumentException(
                "Disease Zone must be a non-empty valid Polygon using SRID 4326.",
                parameterName);
        }

        return (Polygon)polygon.Copy();
    }
}
