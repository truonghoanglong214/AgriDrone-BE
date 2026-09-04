using AgriDrone.Api.Contracts.Farms;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace AgriDrone.Api.Mapping;

internal static class GeoJsonGeometryMapper
{
    private const int Wgs84Srid = 4326;

    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: Wgs84Srid);

    public static Point? ToPoint(GeoJsonPointRequest? request)
    {
        if (request is null)
        {
            return null;
        }

        return GeometryFactory.CreatePoint(
            new Coordinate(
                request.Coordinates[0],
                request.Coordinates[1]));
    }

    public static Polygon? ToPolygon(GeoJsonPolygonRequest? request)
    {
        if (request is null)
        {
            return null;
        }

        var rings = request.Coordinates
            .Select(ToLinearRing)
            .ToArray();

        return GeometryFactory.CreatePolygon(
            rings[0],
            rings.Skip(1).ToArray());
    }

    public static GeoJsonPointResponse? FromPoint(Point? point)
    {
        if (point is null)
        {
            return null;
        }

        return new GeoJsonPointResponse(
            "Point",
            [point.X, point.Y]);
    }

    public static GeoJsonPolygonResponse? FromPolygon(Polygon? polygon)
    {
        if (polygon is null)
        {
            return null;
        }

        var rings = new double[polygon.NumInteriorRings + 1][][];
        rings[0] = ToPositions(polygon.ExteriorRing);

        for (var index = 0; index < polygon.NumInteriorRings; index++)
        {
            rings[index + 1] = ToPositions(polygon.GetInteriorRingN(index));
        }

        return new GeoJsonPolygonResponse("Polygon", rings);
    }

    private static LinearRing ToLinearRing(double[][] positions)
    {
        var coordinates = positions
            .Select(position => new Coordinate(position[0], position[1]))
            .ToArray();

        return GeometryFactory.CreateLinearRing(coordinates);
    }

    private static double[][] ToPositions(LineString ring) =>
        ring.Coordinates
            .Select(coordinate => new[] { coordinate.X, coordinate.Y })
            .ToArray();
}
